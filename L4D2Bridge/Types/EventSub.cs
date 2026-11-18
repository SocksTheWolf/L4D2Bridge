using System;

namespace L4D2Bridge.Types
{
  public enum EventSubEventType
  {
    None,
    Message,
    Cheer,
    Redeem,
  }
  public readonly struct EventSubEvent(Object inData, EventSubEventType inType)
  {
    public readonly EventSubEventType type = inType;
    public readonly Object data = inData;
  }
}
