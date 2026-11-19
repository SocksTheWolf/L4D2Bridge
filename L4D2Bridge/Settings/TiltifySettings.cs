using L4D2Bridge.Models;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace L4D2Bridge.Settings
{
  using RequiredFieldContainer = List<string>;

  /*** Settings for Tiltify ***/
  [JsonObject(MemberSerialization.OptOut, ItemRequired = Required.Always)]
  public class TiltifySettings : SettingsVerifier
  {
    public bool Enabled { get; set; } = false;
    // Settings
    public bool IsTeamCampaign { get; set; } = false;
    public string CampaignID { get; set; } = string.Empty;
    // https://github.com/Tiltify/api/issues/9 (it's 5, and I will come after you if you limit me)
    public int PollingInterval { get; set; } = 5;

    // Auth
    public string ClientID { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    public override void AddRequiredFields(ref RequiredFieldContainer RequiredFieldObj)
    {
      if (Enabled)
        RequiredFieldObj.AddRange([ClientID, ClientSecret, CampaignID]);
    }
  }
}
