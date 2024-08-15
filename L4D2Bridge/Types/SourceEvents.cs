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
        public readonly SourceEventType Type = SourceEventType.None;
        public double Amount = 0.0;
        public string Message = string.Empty;
        public string Name = string.Empty;
        // Only valid if type is Donation
        public string Currency = string.Empty;
        // Twitch Channel
        public string Channel = string.Empty;

        public SourceEvent(SourceEventType type)
        {
            Type = type;
        }

        public override string ToString()
        {
            return $"SourceEvent[{Enum.GetName(typeof(SourceEventType), Type)}] from {Name}, amount {Amount}{Currency} - msg {Message}";
        }
    }

    // A delegate signature of how events should be handled when they are fired.
    public delegate void SourceEventHandler(SourceEvent obj);
}
