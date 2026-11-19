using L4D2Bridge.Types;

namespace L4D2Bridge.Commands
{
  public class RespawnAllPlayers : L4D2CommandBase
  {
    public RespawnAllPlayers(string InSender) : base(ServerCommands.RespawnPlayers, InSender)
    {
      Command = $"sm_bridge_respawnall {Sender}";
    }

    protected override string GetSuccessString()
    {
      return "[Bridge] respawned survivors";
    }
  }
}
