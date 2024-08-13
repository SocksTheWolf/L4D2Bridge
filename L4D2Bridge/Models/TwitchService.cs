using L4D2Bridge.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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

        // Raffle Data
        private const string WinnerLogFile = "raffle.txt";
        private bool RaffleOpen = false;
        private string CurrentRafflePrize = string.Empty;
        private Collection<string> Entries = new();

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
            client.OnLeftChannel += OnChannelLeft;

            if (settings.Events.OnCommand || settings.EnableRaffles)
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

            List<string> ChannelsToConnect = [.. settings.Channels];
            ConnectionCredentials creds = new(settings.BotUserName, settings.OAuthToken);
            client.Initialize(creds, ChannelsToConnect);
            if (client.Connect())
                PrintMessage("Twitch Connected!");
            else
                PrintMessage("Twitch could not connect!");
        }

        public void JoinChannels(TwitchSettings NewSettings)
        {
            if (!client.IsConnected)
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

        /*** Raffle Support ***/
        public void StartRaffle(string rafflePrize)
        {
            // If the raffle prize string is just empty, skip the command
            if (string.IsNullOrWhiteSpace(rafflePrize))
                return;

            if (!settings.EnableRaffles)
                return;

            RaffleOpen = true;
            Entries.Clear();
            CurrentRafflePrize = rafflePrize;
            SendMessageToAllChannels($"Raffle is now open for {CurrentRafflePrize}! Type !enter to enter.");
            PrintMessage($"Raffle has now opened for {CurrentRafflePrize}!");
        }

        public void PickRaffle()
        {
            if (!settings.EnableRaffles)
                return;

            if (Entries.Count <= 0)
            {
                PrintMessage("There are no entries into the raffle, cannot pick a winner!");
                return;
            }

            RaffleOpen = false;

            // Choose a winner
            int ChooseIndex = rng.Next(Entries.Count);
            string WinnerName = Entries[ChooseIndex];
            // Remove this selected winner, because if we have to reroll, then this person won't be a potential choice.
            Entries.RemoveAt(ChooseIndex);

            // Print a message and send it to everyone.
            PrintMessage($"Winner picked {WinnerName} at index {ChooseIndex}");
            SendMessageToAllChannels($"Raffle winner of {CurrentRafflePrize} is @{WinnerName}! Check your Twitch Whispers for info!");

            // Print out the winner to a log file.
            using (StreamWriter FileWriter = File.AppendText(WinnerLogFile))
            {
                FileWriter.WriteLine($"{CurrentRafflePrize} winner is {WinnerName}");
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

            if (loweredCommand == "enter" && settings.EnableRaffles)
            {
                // If raffles are opened and they haven't entered yet,
                // enter the user
                if (RaffleOpen && !Entries.Contains(user))
                {
                    Entries.Add(user);
                    if (settings.RespondToRaffleEntry)
                        SendMessageToChannel(args.Command.ChatMessage.Channel, $"@{user} you have entered!");
                }

                return;
            }

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

        public void SendMessageToChannel(JoinedChannel channel, string message)
        {
            try
            {
                client.SendMessage(channel, message);
            }
            catch (Exception ex)
            {
                PrintMessage($"Encountered exception upon sending message to channel[{channel.Channel}]: {ex}");
            }
        }

        public void SendMessageToAllChannels(string message)
        {
            IReadOnlyList<JoinedChannel> AllJoinedChannels = client.JoinedChannels;
            if (AllJoinedChannels.Count <= 0 || string.IsNullOrWhiteSpace(message))
                return;

            foreach (JoinedChannel channel in AllJoinedChannels)
                SendMessageToChannel(channel, message);
        }
    }
}
