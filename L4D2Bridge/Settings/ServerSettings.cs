using L4D2Bridge.Models;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace L4D2Bridge.Settings
{
  using RequiredFieldContainer = List<string>;

  /*** Settings for a server ***/
  [JsonObject(MemberSerialization.OptOut, ItemRequired = Required.Always)]
  public class ServerSettings : SettingsVerifier
  {
    [JsonProperty(Required = Required.Always)]
    public string ServerIP { get; set; } = string.Empty;

    [JsonProperty(Required = Required.Always)]
    public int ServerPort { get; set; } = 27015;

    [JsonProperty(Required = Required.Always)]
    public string Password { get; set; } = string.Empty;

    // Maximum amount of times to retry a task
    [JsonProperty]
    public int MaxCommandAttempts = 10;

    public override void AddRequiredFields(ref RequiredFieldContainer RequiredFieldObj)
    {
      RequiredFieldObj.AddRange([ServerIP, Password]);
    }
  }
}
