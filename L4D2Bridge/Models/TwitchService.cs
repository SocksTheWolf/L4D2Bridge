using L4D2Bridge.Types;
using L4D2Bridge.Settings;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Charity;
using TwitchLib.Api.Helix.Models.Charity.GetCharityCampaignDonations;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

#pragma warning disable CS1998

namespace L4D2Bridge.Models
{
  public class TwitchService : BaseServiceTickable
  {
    private readonly TwitchClient? client = null;
    private readonly TwitchSettings settings;
    private readonly TwitchAPI api = new();
    private readonly Random rng = new(Guid.NewGuid().GetHashCode());
    internal readonly HttpClient httpClient = new();

    // Auth data
    internal TwitchAuthData auth = new();

    // EventSub System
    private readonly EventSubService? wsEventSub;

    // EventSub Cancel Token
    private readonly CancellationTokenSource cancelToken = new();

    public override string GetWorkflow() => "twitch";
    public override ConsoleSources GetSource() => ConsoleSources.Twitch;

    public TwitchService(TwitchSettings InSettings, IHostedService service)
    {
      settings = InSettings;
      wsEventSub = service as EventSubService;

      httpClient.DefaultRequestHeaders.Accept.Clear();
      httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
      httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.TwitchAuthKey}");

      if (settings.Events.UsesChatFeatures())
      {
        client = new TwitchClient();

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
    protected override bool UsesAsyncStart() => true;

    protected override async Task<bool> Internal_Start_Async()
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

      if (await RefreshAuthToken() == false)
      {
        PrintMessage("Failed to get Twitch Auth Refresh Token");
        return false;
      }

      if (settings.Events.OnCharityDonation || settings.Events.UsesEventSub())
      {
        PrintMessage("Starting Twitch async task thread");
        StartTick();
      }

      if (client != null)
      {
        var credentials = new ConnectionCredentials(settings.BotUserName, $"oauth:{auth.OAuthToken}");
        client.Initialize(credentials);

        if (await client.ConnectAsync())
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

    private async Task Client_OnDisconnected(object? sender, TwitchLib.Client.Events.OnDisconnectedArgs e)
    {
      PrintMessage($"Twitch Disconnected!!");
    }
    private async Task Client_OnConnectionError(object? sender, OnConnectionErrorArgs e)
    {
      PrintMessage($"Twitch Connection Error {e.Error.Message}");
    }

    private async Task Client_OnConnected(object? sender, TwitchLib.Client.Events.OnConnectedEventArgs e)
    {
      PrintMessage("Twitch Connected!");
      await SyncChannels(settings.Channels);
    }

    public async Task<bool> RefreshAuthToken()
    {
      PrintMessage("Fetching Twitch Tokens...");
      var response = await httpClient.PostAsync("https://make.twitchauth.work/get", null);
      if (response.IsSuccessStatusCode)
      {
        var rawResponse = await response.Content.ReadAsStringAsync();
        var data = JsonConvert.DeserializeObject<TwitchAuthData>(rawResponse);
        if (data != null)
        {
          auth = data;
          api.Settings.ClientId = auth.ClientID;
          api.Settings.AccessToken = auth.OAuthToken;

          wsEventSub?.RunConfiguration(auth);
          if (settings.Events.UsesEventSub() && wsEventSub?.isConnected == true)
          {
            try
            {
              PrintMessage("Reconnecting EventSub...");
              await wsEventSub.StopAsync(cancelToken.Token);
              await wsEventSub.StartAsync(cancelToken.Token);
            }
            catch (Exception ex)
            {
              PrintMessage($"Failed to reconnect EventSub {ex}");
            }
          }
          return true;
        }
      }
      return false;
    }

    public void JoinChannels(TwitchSettings NewSettings)
    {
      Task.Run(() => SyncChannels(NewSettings.Channels)).GetAwaiter().GetResult();
    }

    private async Task SyncChannels(string[] Channels)
    {
      if (client == null || !client.IsConnected)
        return;

      // GetJoinedChannel throws exceptions unless we have channels we've
      // already joined. If we haven't joined any channels, then just join
      // all of them.
      if (client.JoinedChannels.Count < 1)
      {
        PrintMessage($"Attempting to join {Channels.Length} channels...");
        foreach (string channel in Channels)
          await client.JoinChannelAsync(channel);

        return;
      }

      // Otherwise, if we have already joined channels, only join the ones we haven't
      // joined before.
      foreach (string channel in Channels)
      {
        // Figure out if we haven't joined this channel previously and join it.
        if (client.GetJoinedChannel(channel) == null)
        {
          PrintMessage($"Attempting to join channel {channel}...");
          await client.JoinChannelAsync(channel);
        }
      }

      // Reconcile any channels we were in, and part the channel.
      var ChannelsToLeave = client.JoinedChannels.Where(channel => { return Channels.Contains(channel.Channel) == false; });
      int NumChannels = ChannelsToLeave.Count();
      if (NumChannels > 0)
      {
        PrintMessage($"There are {NumChannels} twitch channels to leave");
        foreach (JoinedChannel leavingChannel in ChannelsToLeave)
        {
          PrintMessage($"Attempting to leave channel {leavingChannel.Channel}...");
          await client.LeaveChannelAsync(leavingChannel);
        }
      }
    }

    /*** Handle Twitch Events ***/
    private async Task OnChannelJoined(object unused, OnJoinedChannelArgs args)
    {
      PrintMessage($"Joined channel: {args.Channel}");
    }

    private async Task OnChannelLeft(object unused, OnLeftChannelArgs args)
    {
      PrintMessage($"Left channel: {args.Channel}");
    }

    private async Task OnCommandReceived(object unused, OnChatCommandReceivedArgs args)
    {
      string loweredCommand = args.Command.Name.ToLower();
      string user = args.ChatMessage.Username.ToLower();

      if (!settings.Events.OnCommand || settings.ChatCommandPercentChance < 1 || rng.Next(1, 101) > settings.ChatCommandPercentChance)
        return;

      string channel = args.ChatMessage.Channel;
      PrintMessage($"Command Sent from {channel}: {loweredCommand} by {user}");
      Invoke(new SourceEvent(SourceEventType.ChatCommand)
      {
        Channel = channel,
        Message = loweredCommand,
        Name = user,
      });
    }

    private async Task OnChannelRaided(object unused, OnRaidNotificationArgs args)
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

    private async Task OnNewSubscription(object unused, OnNewSubscriberArgs args)
    {
      string planName = args.Subscriber.MsgParamSubPlanName;
      int numMonths = args.Subscriber.MsgParamCumulativeMonths;

      PrintMessage($"Channel subscription for {args.Channel} from {args.Subscriber.DisplayName} of {planName} for {numMonths}!");
      Invoke(new SourceEvent(SourceEventType.Subscription)
      {
        Channel = args.Channel,
        Amount = numMonths,
        Name = args.Subscriber.Login,
        Message = planName
      });
    }

    private async Task OnResubscription(object unused, OnReSubscriberArgs args)
    {
      string planName = args.ReSubscriber.MsgParamSubPlanName;
      int numMonths = args.ReSubscriber.MsgParamCumulativeMonths;

      PrintMessage($"Channel resubscription for {args.Channel} from {args.ReSubscriber.DisplayName} of {planName} for {numMonths}!");
      Invoke(new SourceEvent(SourceEventType.Resubscription)
      {
        Channel = args.Channel,
        Amount = numMonths,
        Name = args.ReSubscriber.Login,
        Message = planName
      });
    }

    private async Task OnPrimePaidSubscription(object unused, OnPrimePaidSubscriberArgs args)
    {
      PrintMessage($"Channel prime subscription for {args.Channel} from {args.PrimePaidSubscriber.DisplayName}!");
      Invoke(new SourceEvent(SourceEventType.Subscription)
      {
        Channel = args.Channel,
        Name = args.PrimePaidSubscriber.Login,
        Amount = 1,
        Message = "prime"
      });
    }

    private async Task OnContinuedGiftSub(object unused, OnContinuedGiftedSubscriptionArgs args)
    {
      PrintMessage($"Channel resubscription for {args.Channel} from {args.ContinuedGiftedSubscription.DisplayName}!");
      Invoke(new SourceEvent(SourceEventType.Resubscription)
      {
        Channel = args.Channel,
        Name = args.ContinuedGiftedSubscription.Login,
      });
    }

    private async Task OnGiftedSubscription(object unused, OnGiftedSubscriptionArgs args)
    {
      string recipient = args.GiftedSubscription.MsgParamRecipientUserName;
      int numMonths = args.GiftedSubscription.MsgParamMultiMonthGiftDuration;

      PrintMessage($"Channel gift subscription for {args.Channel} from {args.GiftedSubscription.DisplayName} to {recipient} for {numMonths}!");
      Invoke(new SourceEvent(SourceEventType.GiftSubscription)
      {
        Channel = args.Channel,
        Name = args.GiftedSubscription.Login,
        Amount = numMonths,
        Message = recipient
      });
    }

    private async Task OnMultiGiftSubscription(object unused, OnCommunitySubscriptionArgs args)
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
      // This is primarily used for Twitch Charity endpoints
      string LastDonationRead = string.Empty;

      // Lambda for writing the last donation read.
      Action<GetCharityCampaignDonationsResponse> WriteLastDonation = Response => {
        if (Response.Data.Length > 0)
          LastDonationRead = Response.Data[0].Id;
      };

      using PeriodicTimer timer = new(TimeSpan.FromSeconds(settings.CharityPollingInterval));
      if (wsEventSub != null)
        await wsEventSub.StartAsync(cancelToken.Token);
      while (ShouldRun)
      {
        // Run Charity Stuff
        if (settings.Events.OnCharityDonation)
        {
          try
          {
            var resp = await api.Helix.Charity.GetCharityCampaignDonationsAsync(auth.UserID);

            // Handle initial case
            if (LastDonationRead == string.Empty)
            {
              WriteLastDonation(resp);
              PrintMessage($"Established baseline donation {LastDonationRead}");
            }
            else
            {
              foreach (var Donation in resp.Data)
              {
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
            PrintMessage($"Twitch Charity Loop hit exception: {ex}");
          }

        }

        if (settings.Events.UsesEventSub() && wsEventSub != null)
        {
          string userName;
          if (!wsEventSub.eventQueue.IsEmpty)
          {
            if (wsEventSub.eventQueue.TryDequeue(out EventSubEvent ev))
            {
              userName = string.Empty;
              switch (ev.type)
              {
                case EventSubEventType.Cheer:
                  {
                    if (ev.data is not ChannelBitsUse payloadData)
                      continue;

                    userName = payloadData.UserLogin;
                    Invoke(new SourceEvent(SourceEventType.Cheer)
                    {
                      Amount = payloadData.Bits,
                      Name = payloadData.UserLogin,
                    });
                  }
                break;

                case EventSubEventType.Redeem:
                  {
                    if (ev.data is not ChannelPointsCustomRewardRedemption payloadData)
                      continue;

                    userName = payloadData.UserLogin;
                    Invoke(new SourceEvent(SourceEventType.PointRedeem)
                    {
                      RedeemID = payloadData.Reward.Id,
                      Name = payloadData.UserLogin,
                      Message = payloadData.Reward.Title
                    });
                  }
                break;

              }
              PrintMessage($"Got new Twitch EventSub from {userName} was {ev.type}");
            }
          }
        }
        await timer.WaitForNextTickAsync(default);
      }
      if (wsEventSub != null)
        await wsEventSub.StopAsync(cancelToken.Token);
    }

    /*** Sending messages to a channel ***/
    public async Task SendMessageToChannel(string channel, string message)
    {
      try
      {
        if (client != null)
          await client.SendMessageAsync(channel, message);
      }
      catch (Exception ex)
      {
        PrintMessage($"Encountered exception upon sending message to channel[{channel}]: {ex}");
      }
    }

    public async Task SendMessageToChannel(JoinedChannel channel, string message)
    {
      try
      {
        if (client != null)
          await client.SendMessageAsync(channel, message);
      }
      catch (Exception ex)
      {
        PrintMessage($"Encountered exception upon sending message to channel[{channel.Channel}]: {ex}");
      }
    }

    public void SendMessageToAllChannels(string message)
    {
      Task.Run(() => SendMessageToAllChannelsAsync(message)).GetAwaiter().GetResult();
    }

    internal async Task SendMessageToAllChannelsAsync(string message)
    {
      if (client == null)
        return;

      IReadOnlyList<JoinedChannel> AllJoinedChannels = client.JoinedChannels;
      if (AllJoinedChannels.Count <= 0 || string.IsNullOrWhiteSpace(message))
        return;

      foreach (JoinedChannel channel in AllJoinedChannels)
        await SendMessageToChannel(channel, message);
    }
  }
}
#pragma warning restore CS1998
