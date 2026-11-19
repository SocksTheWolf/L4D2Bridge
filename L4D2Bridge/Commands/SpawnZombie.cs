using L4D2Bridge.Types;

namespace L4D2Bridge.Commands
{
  public class SpawnZombieCommand : L4D2CommandBase
  {
    private readonly string ZombieType;
    public SpawnZombieCommand(string InZombieType, string InSender) : base(ServerCommands.SpawnZombie, InSender)
    {
      IsSpawner = true;
      ZombieType = InZombieType.ToLower();
      Command = $"sm_bridge_spawnzombie {ZombieType} {Sender}";
    }

    public override string ToString()
    {
      return base.ToString() + $", Zombie[{ZombieType}]";
    }

    protected override string GetSuccessString()
    {
      return "[Bridge] infected spawn success";
    }
  }
}
