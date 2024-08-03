using System;

namespace L4D2Bridge.Types
{
    public enum EventType
    {
        None,
        Donation,
        Resubscription,
        Follow,
        Subscription,
        Raid,
        Bits,
        ChatMessage,
        ChatCommand
    }

    public class SourceEvent
    {
        public EventType Type = EventType.None;
        public double Amount = 0.0;
        public string Message = string.Empty;
        public string Name = string.Empty;

        public SourceEvent(EventType type, string name, double amount, string message)
        {
            Type = type;
            Amount = amount;
            Message = message;
            Name = name;
        }

        public override string ToString()
        {
            return $"Type[{Enum.GetName(typeof(EventType), Type)}] from {Name}, amount {Amount} - msg {Message}";
        }
    }
}
