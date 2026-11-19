using Newtonsoft.Json;

namespace L4D2Bridge.Settings
{
  [JsonObject(MemberSerialization.OptOut)]
  public class DebugSettings
  {
    public bool ShowExceptionDetails { get; set; } = false;
  }
}
