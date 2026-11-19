using L4D2Bridge.Types;

namespace L4D2Bridge.Commands
{
  public class SpawnMobCommand : L4D2CommandBase
  {
    private readonly int NumZombies = 0;
    public SpawnMobCommand(int Amount, string InSender) : base(ServerCommands.SpawnMob, InSender)
    {
      IsSpawner = true;
      NumZombies = Amount;
      Command = $"sm_bridge_spawnmob {Amount} {Sender}";
    }

    public override string ToString()
    {
      return base.ToString() + $", Number[{NumZombies}]";
    }

    protected override string GetSuccessString()
    {
      return "[Bridge] mob spawn success";
    }
  }
}
