using L4D2Bridge.Types;

namespace L4D2Bridge.Commands
{
  public class HealAllPlayers : L4D2CommandBase
  {
    private readonly int HealthAmount;
    public HealAllPlayers(int InHealthAmount, string InSender) : base(ServerCommands.HealPlayers, InSender)
    {
      HealthAmount = InHealthAmount;
      Command = $"sm_bridge_healall {HealthAmount} {Sender}";
    }

    public override string ToString()
    {
      return base.ToString() + $", Amount[{HealthAmount}]";
    }

    protected override string GetSuccessString()
    {
      return "[Bridge] health healed";
    }
  }
}
