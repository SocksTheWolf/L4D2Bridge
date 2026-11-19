using L4D2Bridge.Types;

namespace L4D2Bridge.Commands
{
  public class GetUpPlayers : L4D2CommandBase
  {
    public GetUpPlayers(string InSender) : base(ServerCommands.UnincapPlayers, InSender)
    {
      Command = $"sm_bridge_uncap {Sender}";
    }

    protected override string GetSuccessString()
    {
      return "[Bridge] uppies survivors";
    }
  }
}
