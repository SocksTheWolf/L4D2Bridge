using L4D2Bridge.Types;

namespace L4D2Bridge.Commands
{
  public class SpawnLootboxCommand : L4D2CommandBase
  {
    public SpawnLootboxCommand(string InSender) : base(ServerCommands.SpawnLootbox, InSender)
    {
      IsSpawner = true;
      Command = $"sm_bridge_spawnlootbox {Sender}";
    }

    protected override string GetSuccessString()
    {
      return "[Bridge] lootbox spawned";
    }
  }
}
