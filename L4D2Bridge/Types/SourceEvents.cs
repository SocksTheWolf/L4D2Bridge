using System;

namespace L4D2Bridge.Types
{
  public enum SourceEventType
  {
    None,
    Donation,
    [SEInfo(Alias = "sub")]
    Subscription,
    [SEInfo(Alias = "resub")]
    Resubscription,
    [SEInfo(Alias = "gift sub")]
    GiftSubscription,
    [SEInfo(Alias = "gift subs")]
    MultiGiftSubscription,
    Raid,
    [SEInfo(Alias = "chat")]
    ChatCommand,
    [SEInfo(Alias = "bits")]
    Cheer,
    [SEInfo(Alias = "redeem")]
    PointRedeem
  }

  [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
  public class SEInfo : Attribute
  {
    // A simple name to be used to specify the key
    public string Alias = string.Empty;
  }

  public class SourceEvent(SourceEventType type)
  {
    public readonly SourceEventType Type = type;
    public double Amount = 0.0;
    public string Message = string.Empty;
    public string Name = string.Empty;
    // Only valid if type is Donation
    public string Currency = string.Empty;
    // Twitch Channel
    public string Channel = string.Empty;
    // Only valid if type is PointRedeem
    public string RedeemID = string.Empty;

    public override string ToString()
    {
      return $"SourceEvent[{Enum.GetName(typeof(SourceEventType), Type)}] from {Name}, amount {Amount}{Currency} - msg {Message}";
    }
  }

  // A delegate signature of how events should be handled when they are fired.
  public delegate void SourceEventHandler(SourceEvent obj);
}
