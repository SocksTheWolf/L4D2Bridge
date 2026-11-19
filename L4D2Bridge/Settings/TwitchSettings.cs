using Newtonsoft.Json;
using System.Collections.Generic;

namespace L4D2Bridge.Settings
{
  using RequiredFieldContainer = List<string>;

  /*** Settings for Twitch ***/
  [JsonObject(MemberSerialization.OptOut, ItemRequired = Required.Always)]
  public class TwitchEvents
  {
    public bool OnCommand { get; set; } = false;
    public bool OnRaid { get; set; } = false;
    public bool OnSubscription { get; set; } = false;
    public bool OnResubscription { get; set; } = false;
    public bool OnGiftSubscription { get; set; } = false;
    public bool OnMultiGiftSubscription { get; set; } = false;
    public bool OnCharityDonation { get; set; } = false;
    public bool OnBits { get; set; } = false;
    public bool OnChannelRedeem { get; set; } = false;
    public bool UsesChatFeatures() => OnCommand || OnRaid || OnSubscription || OnResubscription || OnGiftSubscription || OnMultiGiftSubscription;
    public bool UsesEventSub() => OnBits || OnChannelRedeem;
  }

  [JsonObject(MemberSerialization.OptOut, ItemRequired = Required.Always)]
  public class TwitchSettings : SettingsVerifier
  {
    public bool Enabled { get; set; } = false;
    public string[] Channels { get; set; } = [];
    public string BotUserName { get; set; } = string.Empty;
    public string TwitchAuthKey { get; set; } = string.Empty;
    // If the resulting actions from twitch events should be redirected to chat as well.
    public bool PostEventActionsToChat { get; set; } = false;
    // Whether to message when Tiltify events should message into all connected chats
    public bool PostMessageOnTiltifyDonations { get; set; } = false;
    public int ChatCommandPercentChance { get; set; } = 50;

    public int CharityPollingInterval { get; set; } = 10;
    public TwitchEvents Events { get; set; } = new TwitchEvents();

    public override void AddRequiredFields(ref RequiredFieldContainer RequiredFieldObj)
    {
      if (Enabled)
      {
        if (Channels != null && Channels.Length > 0)
          RequiredFieldObj.AddRange(Channels);

        RequiredFieldObj.AddRange([BotUserName, TwitchAuthKey]);
      }
    }
  }
}
