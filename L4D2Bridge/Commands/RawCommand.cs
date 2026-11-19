using L4D2Bridge.Types;
using System;

namespace L4D2Bridge.Commands
{
  public class RawCommand : L4D2CommandBase
  {
    public RawCommand(string InCommand) : base(ServerCommands.Raw)
    {
      Command = InCommand;
    }

    public override string ToString()
    {
      return $"Command[{Enum.GetName(typeof(ServerCommands), Type)}], Args[{Command}]";
    }
  }
}
