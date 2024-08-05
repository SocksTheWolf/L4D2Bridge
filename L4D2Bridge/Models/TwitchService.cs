using L4D2Bridge.Types;
using System;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace L4D2Bridge.Models
{
    public class TwitchService : BaseService
    {
        private readonly TwitchClient client;
        private readonly TwitchSettings settings;
        private Random rng = new();

        public override string GetWorkflow() => "twitch";
        public override ConsoleSources GetSource() => ConsoleSources.Twitch;

        public TwitchService(TwitchSettings InSettings)
        {
            settings = InSettings;
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };

            WebSocketClient customClient = new(clientOptions);

            client = new TwitchClient(customClient)
            {
                AutoReListenOnException = true
            };

#pragma warning disable CS8622
            client.OnJoinedChannel += OnChannelJoined;

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

        public override void Start()
        {
            if (settings.Channels == null)
            {
                PrintMessage("Twitch service is missing channels to connect to!!!");
                return;
            }

            PrintMessage($"Twitch Initialized for {settings.Channels.Length} channels");
            ConnectionCredentials creds = new(settings.BotUserName, settings.OAuthToken);
            client.Initialize(creds);
            if (client.Connect())
            {
                PrintMessage("Twitch Connected!");
                foreach (string channel in settings.Channels)
                {
                    client.JoinChannel(channel);
                }
            }
        }
        private void OnChannelJoined(object unused, OnJoinedChannelArgs args)
        {
            PrintMessage($"Joined channel: {args.Channel}");
        }

        private void OnCommandReceived(object unused, OnChatCommandReceivedArgs args)
        {
            if (rng.Next(1, 101) > settings.ChatCommandPercentChance)
                return;

            Invoke(new SourceEvent(SourceEventType.ChatCommand, args.Command.ChatMessage.Username, 
                args.Command.ChatMessage.Channel, args.Command.CommandText));
        }

        private void OnChannelRaided(object unused, OnRaidNotificationArgs args)
        {
            string fromUser = args.RaidNotification.MsgParamLogin;
            if (!int.TryParse(args.RaidNotification.MsgParamViewerCount, out int viewerCount))
                viewerCount = 1;

            PrintMessage($"Channel raid for {args.Channel} from {fromUser} of {viewerCount} viewers!");
            Invoke(new SourceEvent(SourceEventType.Raid, fromUser, args.Channel, viewerCount, string.Empty));
        }

        private void OnNewSubscription(object unused, OnNewSubscriberArgs args)
        {
            string planName = args.Subscriber.SubscriptionPlanName;
            if (!int.TryParse(args.Subscriber.MsgParamCumulativeMonths, out int numMonths))
                numMonths = 0;

            PrintMessage($"Channel subscription for {args.Channel} from {args.Subscriber.DisplayName} of {planName} for {numMonths}!");
            Invoke(new SourceEvent(SourceEventType.Subscription, args.Subscriber.Login, args.Channel, numMonths, planName));
        }

        private void OnResubscription(object unused, OnReSubscriberArgs args)
        {
            string planName = args.ReSubscriber.SubscriptionPlanName;

            if (!int.TryParse(args.ReSubscriber.MsgParamCumulativeMonths, out int numMonths))
                numMonths = 1;

            PrintMessage($"Channel resubscription for {args.Channel} from {args.ReSubscriber.DisplayName} of {planName} for {numMonths}!");
            Invoke(new SourceEvent(SourceEventType.Resubscription, args.ReSubscriber.Login, args.Channel, numMonths, 
                args.ReSubscriber.SubscriptionPlanName));
        }

        private void OnPrimePaidSubscription(object unused, OnPrimePaidSubscriberArgs args)
        {
            string planName = args.PrimePaidSubscriber.SubscriptionPlanName;

            if (!int.TryParse(args.PrimePaidSubscriber.MsgParamCumulativeMonths, out int numMonths))
                numMonths = 1;

            PrintMessage($"Channel resubscription for {args.Channel} from {args.PrimePaidSubscriber.DisplayName} of {planName} for {numMonths}!");
            Invoke(new SourceEvent(SourceEventType.Resubscription, args.PrimePaidSubscriber.Login, args.Channel, numMonths, 
                args.PrimePaidSubscriber.SubscriptionPlanName));
        }

        private void OnContinuedGiftSub(object unused, OnContinuedGiftedSubscriptionArgs args)
        {
            PrintMessage($"Channel resubscription for {args.Channel} from {args.ContinuedGiftedSubscription.DisplayName}!");
            Invoke(new SourceEvent(SourceEventType.Resubscription, args.ContinuedGiftedSubscription.Login, args.Channel, string.Empty));
        }

        private void OnGiftedSubscription(object unused, OnGiftedSubscriptionArgs args)
        {
            string recipient = args.GiftedSubscription.MsgParamRecipientUserName;
            if (!int.TryParse(args.GiftedSubscription.MsgParamMultiMonthGiftDuration, out int numMonths))
                numMonths = 1;

            PrintMessage($"Channel gift subscription for {args.Channel} from {args.GiftedSubscription.DisplayName} to {recipient} for {numMonths}!");
            Invoke(new SourceEvent(SourceEventType.GiftSubscription, args.GiftedSubscription.Login, args.Channel, numMonths, recipient));
        }

        private void OnMultiGiftSubscription(object unused, OnCommunitySubscriptionArgs args)
        {
            int numGifts = args.GiftedSubscription.MsgParamMassGiftCount;
            PrintMessage($"Channel multigift subscription for {args.Channel} from {args.GiftedSubscription.DisplayName} of {numGifts}!");
            Invoke(new SourceEvent(SourceEventType.GiftSubscription, args.GiftedSubscription.Login, args.Channel, 
                numGifts, args.GiftedSubscription.MsgParamSubPlan.ToString()));
        }

        /*** Sending messages to a channel ***/
        public void SendMessageToChannel(string channel, string message)
        {
            try
            {
                client.SendMessage(channel, message);
            }
            catch (Exception ex)
            {
                PrintMessage($"Encountered exception upon sending message to channel[{channel}]: {ex}");
            }
        }

        public void SendMessageToAllChannels(string message)
        {
            if (settings.Channels == null || settings.Channels.Length <= 0 || string.IsNullOrWhiteSpace(message))
                return;

            foreach (string channel in settings.Channels)
                SendMessageToChannel(channel, message);
        }
    }
}
