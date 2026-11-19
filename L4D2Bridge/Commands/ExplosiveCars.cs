using L4D2Bridge.Types;

namespace L4D2Bridge.Commands
{
  public class ExplosiveCars : L4D2CommandBase
  {
    public ExplosiveCars(string InSender) : base(ServerCommands.ExplosiveCars, InSender)
    {
      Command = $"sm_bridge_cars_explode {Sender}";
    }

    protected override string GetSuccessString()
    {
      return "[Bridge] started explosive car event";
    }
  }
}
