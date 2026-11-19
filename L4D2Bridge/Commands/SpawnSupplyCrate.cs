using L4D2Bridge.Types;

namespace L4D2Bridge.Commands
{
  public class SpawnSupplyCrateCommand : L4D2CommandBase
  {
    public SpawnSupplyCrateCommand(string InSender) : base(ServerCommands.SpawnSupplyCrate, InSender)
    {
      IsSpawner = true;
      Command = $"sm_bridge_supplycrate {Sender}";
    }

    protected override string GetSuccessString()
    {
      return "[Bridge] supplies spawned";
    }
  }
}
