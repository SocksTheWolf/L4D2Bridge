using System;

namespace L4D2Bridge.Types
{
  // Data that comes from the EventSub channel to pass on to the main Twitch service
  // handler
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
