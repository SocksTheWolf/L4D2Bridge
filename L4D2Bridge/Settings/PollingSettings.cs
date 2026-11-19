using Newtonsoft.Json;

namespace L4D2Bridge.Settings
{
  [JsonObject(MemberSerialization.OptOut, ItemRequired = Required.DisallowNull)]
  public class RequestPollSettings
  {
    public bool Enabled { get; set; } = false;
    public string RequestLocation { get; set; } = string.Empty;
    public int PollingInterval { get; set; } = 5;
  }
}
