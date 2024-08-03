using System;

namespace L4D2Bridge.Types
{
    public enum SourceEventType
    {
        None,
        Donation,
        Subscription,
        Resubscription,
        GiftSubscription,
        MultiGiftSubscription,
        Raid,
        ChatCommand
    }

    public class SourceEvent
    {
        public SourceEventType Type = SourceEventType.None;
        public double Amount = 0.0;
        public string Message = string.Empty;
        public string Name = string.Empty;
        // Twitch Channel
        public string Channel = string.Empty;

        public SourceEvent(SourceEventType type, string name, string message)
        {
            Type = type;
            Message = message;
            Name = name;
        }

        public SourceEvent(SourceEventType type, string name, double amount, string message)
        {
            Type = type;
            Amount = amount;
            Message = message;
            Name = name;
        }

        public SourceEvent(SourceEventType type, string name, string channel, string message)
        {
            Type = type;
            Channel = channel;
            Message = message;
            Name = name;
        }

        public SourceEvent(SourceEventType type, string name, string channel, double amount, string message)
        {
            Type = type;
            Channel = channel;
            Message = message;
            Amount = amount;
            Name = name;
        }

        public override string ToString()
        {
            return $"SourceEvent[{Enum.GetName(typeof(SourceEventType), Type)}] from {Name}, amount {Amount} - msg {Message}";
        }
    }
}
