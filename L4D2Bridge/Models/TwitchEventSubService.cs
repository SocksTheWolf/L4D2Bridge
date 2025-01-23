using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets;
using L4D2Bridge.Types;
using TwitchLib.Api;
using System.Collections.Generic;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Interfaces;

namespace L4D2Bridge.Models
{
    public class TwitchEventSubService : IHostedService
    {
        private readonly ILogger<TwitchEventSubService> _logger;
        private readonly EventSubWebsocketClient _eventSubWebsocketClient;
        public event SourceEventHandler? OnSourceEvent;
        public Action<string>? OnConsolePrint { private get; set; }
        public string TwitchOAuthToken = string.Empty;
        public string TwitchClientID = string.Empty;
        public string TwitchChannelName = string.Empty;
        private string TwitchChannelID = string.Empty;
        private readonly TwitchAPI twitch = new();

        public TwitchEventSubService(ILogger<TwitchEventSubService> logger, EventSubWebsocketClient eventSubWebsocketClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _eventSubWebsocketClient = eventSubWebsocketClient ?? throw new ArgumentNullException(nameof(eventSubWebsocketClient));
            _eventSubWebsocketClient.WebsocketConnected += OnWebsocketConnected;
            _eventSubWebsocketClient.WebsocketDisconnected += OnWebsocketDisconnected;
            _eventSubWebsocketClient.WebsocketReconnected += OnWebsocketReconnected;
            _eventSubWebsocketClient.ErrorOccurred += OnErrorOccurred;
            _eventSubWebsocketClient.ChannelCharityCampaignDonate += OnDonation;
        }

        // Helper function for printing messages to console (via Actions)
        protected void PrintMessage(string message)
        {
            OnConsolePrint?.Invoke(message);
        }

        private async Task OnErrorOccurred(object sender, ErrorOccuredArgs e)
        {
            _logger.LogError($"Websocket {_eventSubWebsocketClient.SessionId} - Error occurred! {e.ToString()}");
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // This does nothing, because we need the settings to be pushed first
        }

        public async Task ConnectAsync(CancellationToken cancelToken)
        {
            twitch.Settings.ClientId = TwitchClientID;
            twitch.Settings.AccessToken = TwitchOAuthToken.Replace("oauth:", "", StringComparison.CurrentCultureIgnoreCase);
            List<string> channelNames = new List<string>() { TwitchChannelName };
            // Go and fetch this.
            var Response = await twitch.Helix.Users.GetUsersAsync(null, channelNames, null);
            TwitchChannelID = Response.Users[0].Id;
            PrintMessage($"Starting the EventSub connection, got channel id {TwitchChannelID}");
            await _eventSubWebsocketClient.ConnectAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _eventSubWebsocketClient.DisconnectAsync();
        }

        private async Task OnWebsocketConnected(object sender, WebsocketConnectedArgs e)
        {
            _logger.LogInformation($"Websocket {_eventSubWebsocketClient.SessionId} connected!");

            //if (!e.IsRequestedReconnect)
            if (true)
            {
                var conditions = new Dictionary<string, string>()
                {
                    // Don't have time to look into this, but this string is getting formatted strangely
                    // which causes the system to fail the connection.
                    { "broadcaster_user_id", $"{TwitchChannelID}" }
                };
                try
                {
                    var response = await twitch.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.charity_campaign.donate", "1", conditions,
                    EventSubTransportMethod.Websocket, _eventSubWebsocketClient.SessionId);
                    _logger.LogInformation($"Subscribed: {response.Subscriptions[0].Status}");
                }
                catch(Exception)
                {
                    PrintMessage("Failed to set up EventSub subscription, please make sure the channel id is correct.");
                    return;
                }
                PrintMessage("Now listening to donation messages");
            }
        }

        private async Task OnWebsocketDisconnected(object sender, EventArgs e)
        {
            PrintMessage("EventSub service disconnected!");
            // Don't do this in production. You should implement a better reconnect strategy
            while (!await _eventSubWebsocketClient.ReconnectAsync())
            {
                _logger.LogError("Websocket reconnect failed!");
                await Task.Delay(1000);
            }
        }

        private async Task OnDonation(object sender, ChannelCharityCampaignDonateArgs args)
        {
            var PayloadData = args.Notification.Payload.Event;
            // Calculate how much the donation was, and convert it into something reasonable.
            int RawCashValue = PayloadData.Amount.Value;
            double DivideBy = Math.Pow(10, PayloadData.Amount.DecimalPlaces);
            double CashValue = RawCashValue / DivideBy;

            string Currency = PayloadData.Amount.Currency;
            PrintMessage($"New Donation Recieved from {PayloadData.UserLogin} for {CashValue} {Currency}");

            Invoke(new SourceEvent(SourceEventType.Donation)
            {
                Amount = CashValue,
                Currency = Currency,
                Name = PayloadData.UserLogin,
                Message = "",
            });
        }

        private async Task OnWebsocketReconnected(object sender, EventArgs e)
        {
            _logger.LogWarning($"Websocket {_eventSubWebsocketClient.SessionId} reconnected");
            PrintMessage("EventSub service has reconnected!");
        }

        // An invoker function that broadcasts to the event delegate that the service has
        // an event trigger.
        protected void Invoke(SourceEvent eventData)
        {
            try
            {
                // Try to push this to a worker thread
                if (!ThreadPool.QueueUserWorkItem(Internal_Invoke, eventData))
                {
                    // If it could not be pushed, then run it on the current thread.
                    OnSourceEvent?.Invoke(eventData);
                }
            }
            catch (NotSupportedException ex)
            {
                _logger.LogError($"C# decided to be really confusing and forget that the `false` value exists for a boolean: {ex}");
            }
        }

        // Internal invoker that uses a threadpool to execute functionality.
        private void Internal_Invoke(object? eventData)
        {
            if (eventData == null)
                return;

            SourceEvent sourceEvent = (SourceEvent)eventData;
            try
            {
                OnSourceEvent?.Invoke(sourceEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to handle Invoke for EventSub: {ex}");
            }
        }
    }
}
