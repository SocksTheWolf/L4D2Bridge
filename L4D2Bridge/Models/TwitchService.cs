using L4D2Bridge.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Models;
using TwitchLib.Communication.Events;
using TwitchLib.Api;
using TwitchLib.Client.Enums;
using TwitchLib.Api.Helix.Models.Charity.GetCharityCampaignDonations;

namespace L4D2Bridge.Models
{
    public class TwitchService : BaseServiceTickable
    {
        private readonly TwitchClient? client = null;
        private readonly TwitchSettings settings;
        private readonly TwitchAPI api = new();
        private Random rng = new Random(Guid.NewGuid().GetHashCode());

        public override string GetWorkflow() => "twitch";
        public override ConsoleSources GetSource() => ConsoleSources.Twitch;

        public TwitchService(TwitchSettings InSettings)
        {
            settings = InSettings;
            api.Settings.ClientId = settings.ClientID;
            api.Settings.AccessToken = settings.OAuthToken;

            if (settings.Events.UsesChatFeatures())
            {
                var options = new ClientOptions();
                client = new TwitchClient(null, ClientProtocol.WebSocket)
                {
                    AutoReListenOnException = true
                };

#pragma warning disable CS8622
                client.OnConnected += Client_OnConnected;
                client.OnDisconnected += Client_OnDisconnected;
                client.OnConnectionError += Client_OnConnectionError;
                client.OnJoinedChannel += OnChannelJoined;
                client.OnLeftChannel += OnChannelLeft;

                if (settings.Events.OnCommand)
                    client.OnChatCommandReceived += OnCommandReceived;

                if (settings.Events.OnRaid)
                    client.OnRaidNotification += OnChannelRaided;

                if (settings.Events.OnSubscription)
                    client.OnNewSubscriber += OnNewSubscription;

                if (settings.Events.OnGiftSubscription)
                    client.OnGiftedSubscription += OnGiftedSubscription;

                if (settings.Events.OnMultiGiftSubscription)
                    client.OnCommunitySubscription += OnMultiGiftSubscription;

                if (settings.Events.OnResubscription)
                {
                    client.OnReSubscriber += OnResubscription;
                    client.OnPrimePaidSubscriber += OnPrimePaidSubscription;
                    client.OnContinuedGiftedSubscription += OnContinuedGiftSub;
                }
#pragma warning restore CS8622
            }
        }

        protected override bool Internal_Start()
        {
            if (settings.Channels == null)
            {
                PrintMessage("Twitch service is missing channels to connect to!!!");
                return false;
            }

            if (!settings.IsValid())
            {
                PrintMessage("Twitch settings are invalid, cannot continue!");
                return false;
            }

            if (settings.Events.OnCharityDonation)
            {
                PrintMessage("Starting the polling for Twitch Donations!");
                StartTick();
            }

            if (client != null)
            {
                List<string> ChannelsToConnect = [.. settings.Channels];
                ConnectionCredentials creds = new(settings.BotUserName, "oauth:" + settings.OAuthToken);

                client.Initialize(creds, ChannelsToConnect);

                if (client.Connect())
                {
                    return true;
                }
                else
                {
                    PrintMessage("Twitch could not connect!");
                    return false;
                }
            }
            return true;
        }

        private void Client_OnDisconnected(object? sender, OnDisconnectedEventArgs e)
        {
            PrintMessage($"Twitch Disconnected!!");
        }

        private void Client_OnConnected(object? sender, OnConnectedArgs e)
        {
            PrintMessage("Twitch Connected!");
        }
        private void Client_OnConnectionError(object? sender, OnConnectionErrorArgs e)
        {
            PrintMessage($"Twitch Connection Error {e.Error.Message}");
        }

        public void JoinChannels(TwitchSettings NewSettings)
        {
            if (client == null || !client.IsConnected)
                return;

            // GetJoinedChannel throws exceptions unless we have channels we've
            // already joined. If we haven't joined any channels, then just join
            // all of them.
            if (client.JoinedChannels.Count < 1)
            {
                PrintMessage($"Attempting to join {NewSettings.Channels.Count()} channels...");
                foreach (string channel in NewSettings.Channels)
                    client.JoinChannel(channel);

                return;
            }

            // Otherwise, if we have already joined channels, only join the ones we haven't
            // joined before.
            foreach (string channel in NewSettings.Channels)
            {
                // Figure out if we haven't joined this channel previously and join it.
                if (client.GetJoinedChannel(channel) == null)
                {
                    PrintMessage($"Attempting to join channel {channel}...");
                    client.JoinChannel(channel);
                }  
            }

            // Reconcile any channels we were in, and part the channel.
            var ChannelsToLeave = client.JoinedChannels.Where((JoinedChannel channel) => { return NewSettings.Channels.Contains(channel.Channel) == false; });
            int NumChannels = ChannelsToLeave.Count();
            if (NumChannels > 0)
            {
                PrintMessage($"There are {NumChannels} twitch channels to leave");
                foreach (JoinedChannel leavingChannel in ChannelsToLeave)
                {
                    PrintMessage($"Attempting to leave channel {leavingChannel.Channel}...");
                    client.LeaveChannel(leavingChannel);
                }
            }
        }

        /*** Handle Twitch Events ***/
        private void OnChannelJoined(object unused, OnJoinedChannelArgs args)
        {
            PrintMessage($"Joined channel: {args.Channel}");
        }

        private void OnChannelLeft(object unused, OnLeftChannelArgs args)
        {
            PrintMessage($"Left channel: {args.Channel}");
        }

        private void OnCommandReceived(object unused, OnChatCommandReceivedArgs args)
        {
            string loweredCommand = args.Command.CommandText.ToLower();
            string user = args.Command.ChatMessage.Username.ToLower();

            if (!settings.Events.OnCommand || settings.ChatCommandPercentChance < 1 || rng.Next(1, 101) > settings.ChatCommandPercentChance)
                return;

            Invoke(new SourceEvent(SourceEventType.ChatCommand)
            {
                Channel = args.Command.ChatMessage.Channel,
                Message = loweredCommand,
                Name = user,
            });
        }

        private void OnChannelRaided(object unused, OnRaidNotificationArgs args)
        {
            string fromUser = args.RaidNotification.MsgParamLogin;
            if (!int.TryParse(args.RaidNotification.MsgParamViewerCount, out int viewerCount))
                viewerCount = 1;

            PrintMessage($"Channel raid for {args.Channel} from {fromUser} of {viewerCount} viewers!");
            Invoke(new SourceEvent(SourceEventType.Raid)
            {
                Channel = args.Channel,
                Amount = viewerCount,
                Name = fromUser,
            });
        }

        private void OnNewSubscription(object unused, OnNewSubscriberArgs args)
        {
            string planName = args.Subscriber.SubscriptionPlanName;
            if (!int.TryParse(args.Subscriber.MsgParamCumulativeMonths, out int numMonths))
                numMonths = 0;

            PrintMessage($"Channel subscription for {args.Channel} from {args.Subscriber.DisplayName} of {planName} for {numMonths}!");
            Invoke(new SourceEvent(SourceEventType.Subscription)
            {
                Channel = args.Channel,
                Amount = numMonths,
                Name = args.Subscriber.Login,
                Message = planName
            });
        }

        private void OnResubscription(object unused, OnReSubscriberArgs args)
        {
            string planName = args.ReSubscriber.SubscriptionPlanName;

            if (!int.TryParse(args.ReSubscriber.MsgParamCumulativeMonths, out int numMonths))
                numMonths = 1;

            PrintMessage($"Channel resubscription for {args.Channel} from {args.ReSubscriber.DisplayName} of {planName} for {numMonths}!");
            Invoke(new SourceEvent(SourceEventType.Resubscription)
            {
                Channel = args.Channel,
                Amount = numMonths,
                Name = args.ReSubscriber.Login,
                Message = args.ReSubscriber.SubscriptionPlanName
            });
        }

        private void OnPrimePaidSubscription(object unused, OnPrimePaidSubscriberArgs args)
        {
            string planName = args.PrimePaidSubscriber.SubscriptionPlanName;

            if (!int.TryParse(args.PrimePaidSubscriber.MsgParamCumulativeMonths, out int numMonths))
                numMonths = 1;

            PrintMessage($"Channel resubscription for {args.Channel} from {args.PrimePaidSubscriber.DisplayName} of {planName} for {numMonths}!");
            Invoke(new SourceEvent(SourceEventType.Resubscription)
            {
                Channel = args.Channel,
                Name = args.PrimePaidSubscriber.Login,
                Amount = numMonths,
                Message = args.PrimePaidSubscriber.SubscriptionPlanName
            });
        }

        private void OnContinuedGiftSub(object unused, OnContinuedGiftedSubscriptionArgs args)
        {
            PrintMessage($"Channel resubscription for {args.Channel} from {args.ContinuedGiftedSubscription.DisplayName}!");
            Invoke(new SourceEvent(SourceEventType.Resubscription)
            {
                Channel = args.Channel,
                Name = args.ContinuedGiftedSubscription.Login,
            });
        }

        private void OnGiftedSubscription(object unused, OnGiftedSubscriptionArgs args)
        {
            string recipient = args.GiftedSubscription.MsgParamRecipientUserName;
            if (!int.TryParse(args.GiftedSubscription.MsgParamMultiMonthGiftDuration, out int numMonths))
                numMonths = 1;

            PrintMessage($"Channel gift subscription for {args.Channel} from {args.GiftedSubscription.DisplayName} to {recipient} for {numMonths}!");
            Invoke(new SourceEvent(SourceEventType.GiftSubscription)
            {
                Channel = args.Channel,
                Name = args.GiftedSubscription.Login,
                Amount = numMonths,
                Message = recipient
            });
        }

        private void OnMultiGiftSubscription(object unused, OnCommunitySubscriptionArgs args)
        {
            int numGifts = args.GiftedSubscription.MsgParamMassGiftCount;
            PrintMessage($"Channel multigift subscription for {args.Channel} from {args.GiftedSubscription.DisplayName} of {numGifts}!");
            Invoke(new SourceEvent(SourceEventType.MultiGiftSubscription)
            {
                Channel = args.Channel,
                Name = args.GiftedSubscription.Login,
                Amount = numGifts,
                Message = args.GiftedSubscription.MsgParamSubPlan.ToString()
            });
        }

        protected override async Task Tick()
        {
            List<string> channels = new List<string>();
            foreach (var channel in settings.Channels) {
                channels.Add(channel);
            }
            var idLookup = await api.Helix.Users.GetUsersAsync(null, channels);
            string TwitchChannelID = idLookup.Users[0].Id;
            string LastDonationRead = string.Empty;

            // Lambda for writing the last donation read.
            Action<GetCharityCampaignDonationsResponse> WriteLastDonation = Response => {
                if (Response.Data.Length > 0)
                    LastDonationRead = Response.Data[0].Id;
            };

            using PeriodicTimer timer = new(TimeSpan.FromSeconds(settings.CharityPollingInterval));
            while (ShouldRun)
            {
                try
                {
                    var resp = await api.Helix.Charity.GetCharityCampaignDonationsAsync(TwitchChannelID);

                    // Handle initial case
                    if (LastDonationRead == string.Empty) {
                        WriteLastDonation(resp);
                        PrintMessage($"Established baseline donation {LastDonationRead}");
                    } else {
                        foreach (var Donation in resp.Data) {
                            if (Donation.Id == LastDonationRead)
                                break;

                            var amount = Donation.Amount;
                            double decPlaces = amount.DecimalPlaces != null ? (double)amount.DecimalPlaces : 0.0;
                            double totalAmount = amount.Value != null ? (double)amount.Value / Math.Pow(10.0, decPlaces) : 0.0;
                            if (totalAmount == 0.0)
                                continue;

                            PrintMessage($"Got new donation of {totalAmount} {amount.Currency} from {Donation.UserLogin}");
                            Invoke(new SourceEvent(SourceEventType.Donation)
                            {
                                Amount = totalAmount,
                                Currency = amount.Currency,
                                Name = Donation.UserLogin,
                                Message = ""
                            });
                        }
                        WriteLastDonation(resp);
                    }
                }
                catch (Exception ex)
                {
                    PrintMessage($"Loop hit exception: {ex}");
                }

                await timer.WaitForNextTickAsync(default);
            }
        }

        /*** Sending messages to a channel ***/
        public void SendMessageToChannel(string channel, string message)
        {
            try
            {
                if (client != null)
                    client.SendMessage(channel, message);
            }
            catch (Exception ex)
            {
                PrintMessage($"Encountered exception upon sending message to channel[{channel}]: {ex}");
            }
        }

        public void SendMessageToChannel(JoinedChannel channel, string message)
        {
            try
            {
                if (client != null)
                    client.SendMessage(channel, message);
            }
            catch (Exception ex)
            {
                PrintMessage($"Encountered exception upon sending message to channel[{channel.Channel}]: {ex}");
            }
        }

        public void SendMessageToAllChannels(string message)
        {
            if (client == null)
                return;

            IReadOnlyList<JoinedChannel> AllJoinedChannels = client.JoinedChannels;
            if (AllJoinedChannels.Count <= 0 || string.IsNullOrWhiteSpace(message))
                return;

            foreach (JoinedChannel channel in AllJoinedChannels)
                SendMessageToChannel(channel, message);
        }
    }
}
