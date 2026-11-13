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
    public struct EventSubEvent
    {
        public EventSubEvent(Object inData, EventSubEventType inType)
        {
            type = inType;
            data = inData;
        }
        public readonly EventSubEventType type;
        public readonly Object data;
    }
}
