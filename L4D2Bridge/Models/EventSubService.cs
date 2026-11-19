using L4D2Bridge.Types;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.Models;

namespace L4D2Bridge.Models
{
  public class EventSubService : IHostedService
  {
    private readonly ILogger<EventSubService> _logger;
    private readonly EventSubWebsocketClient _eventSubWebsocketClient;
    private readonly TwitchAPI _twitchApi = new();
    public ConcurrentQueue<EventSubEvent> eventQueue = new();
    private string _userId = string.Empty;
    public bool isConnected { get; private set; } = false;

    public EventSubService(ILogger<EventSubService> logger, EventSubWebsocketClient eventSubWebsocketClient)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));

      _eventSubWebsocketClient = eventSubWebsocketClient ?? throw new ArgumentNullException(nameof(eventSubWebsocketClient));
      _eventSubWebsocketClient.WebsocketConnected += OnWebsocketConnected;
      _eventSubWebsocketClient.WebsocketDisconnected += OnWebsocketDisconnected;
      _eventSubWebsocketClient.WebsocketReconnected += OnWebsocketReconnected;
      _eventSubWebsocketClient.ErrorOccurred += OnErrorOccurred;

      _eventSubWebsocketClient.UnknownEventSubNotification += OnUnknownEventSubNotification;
      _eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += OnChannelPointRedeem;
      _eventSubWebsocketClient.ChannelBitsUse += OnChannelBitsUsed;
    }

    public void RunConfiguration(TwitchAuthData settings)
    {
      _twitchApi.Settings.ClientId = settings.ClientID;
      _twitchApi.Settings.AccessToken = settings.OAuthToken;
      _userId = settings.UserID;
    }

    private async Task OnErrorOccurred(object? sender, ErrorOccuredArgs e)
    {
      _logger.LogError($"Websocket {_eventSubWebsocketClient.SessionId} - Error occurred!");
    }

    private async Task OnChannelChatMessage(object? sender, ChannelChatMessageArgs e)
    {
      _logger.LogInformation($"@{e.Payload.Event.ChatterUserName} #{e.Payload.Event.BroadcasterUserName}: {e.Payload.Event.Message.Text}");
      eventQueue.Enqueue(new EventSubEvent(e.Payload, EventSubEventType.Message));
    }

    private async Task OnChannelPointRedeem(object? sender, ChannelPointsCustomRewardRedemptionArgs e)
    {
      _logger.LogInformation($"@{e.Payload.Event.UserLogin} #{e.Payload.Event.BroadcasterUserName}: {e.Payload.Event.Reward.Title} Redeemed");
      eventQueue.Enqueue(new EventSubEvent(e.Payload, EventSubEventType.Redeem));
    }

    private async Task OnChannelBitsUsed(object? sender, ChannelBitsUseArgs e)
    {
      _logger.LogInformation($"@{e.Payload.Event.UserLogin} #{e.Payload.Event.BroadcasterUserName}: {e.Payload.Event.Bits} Bits Spent");
      eventQueue.Enqueue(new EventSubEvent(e.Payload, EventSubEventType.Cheer));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
      await _eventSubWebsocketClient.ConnectAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
      await _eventSubWebsocketClient.DisconnectAsync();
    }

    private async Task OnWebsocketConnected(object? sender, WebsocketConnectedArgs e)
    {
      _logger.LogInformation($"Websocket {_eventSubWebsocketClient.SessionId} connected!");

      if (!e.IsRequestedReconnect)
      {
        var condition = new Dictionary<string, string> { { "broadcaster_user_id", _userId }};

        // Create and send EventSubscription
        await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.cheer", "1", condition, EventSubTransportMethod.Websocket, _eventSubWebsocketClient.SessionId);
        await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.channel_points_custom_reward_redemption.add", "1", condition, EventSubTransportMethod.Websocket, _eventSubWebsocketClient.SessionId);
      }
      isConnected = true;
    }


    private async Task OnWebsocketDisconnected(object? sender, WebsocketDisconnectedArgs e)
    {
      _logger.LogError($"Websocket {_eventSubWebsocketClient.SessionId} disconnected!");
      isConnected = false;

      // Don't do this in production. You should implement a better reconnect strategy (nah)
      while (!await _eventSubWebsocketClient.ReconnectAsync())
      {
        _logger.LogError("Websocket reconnect failed!");
        await Task.Delay(1000);
      }
    }

    private async Task OnWebsocketReconnected(object? sender, WebsocketReconnectedArgs e)
    {
      _logger.LogWarning($"Websocket {_eventSubWebsocketClient.SessionId} reconnected");
      isConnected = true;
    }

    // Handling notifications that are not (yet) implemented
    private async Task OnUnknownEventSubNotification(object? sender, UnknownEventSubNotificationArgs e)
    {
      var metadata = (WebsocketEventSubMetadata)e.Metadata;
      _logger.LogInformation("Received event that has not yet been implemented: type:{type}, version:{version}", metadata.SubscriptionType, metadata.SubscriptionVersion);

      switch ((metadata.SubscriptionType, metadata.SubscriptionVersion))
      {
        case ("channel.chat.message", "1"): /*code to handle the event*/ break;
        default: break;
      }
    }
  }
}