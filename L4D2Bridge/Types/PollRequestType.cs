using Newtonsoft.Json;

namespace L4D2Bridge.Types
{
  [JsonObject(MemberSerialization.OptOut, ItemRequired = Required.Always)]
  public class PollRequestDataType
  {
    public int id { get; set; } = -1;
    public string timestamp { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string amount { get; set; } = string.Empty;
  }

  [JsonObject(MemberSerialization.OptOut, ItemRequired = Required.Always)]
  public class PollRequestType
  {
    public PollRequestDataType[] results = [];
  }
}
