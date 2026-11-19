using L4D2Bridge.Models;
using L4D2Bridge.Types;

namespace L4D2Bridge.Commands
{
  public class CheckPauseCommand : L4D2CommandBase
  {
    public CheckPauseCommand() : base(ServerCommands.CheckPause)
    {
      Command = "sm_bridge_checkpause";
      CanRetry = false;
    }

    // NOTE: This is not valid to call unless WasSuccessful() is true
    public bool IsPaused()
    {
      // TODO: Consider throwing if not successful
      if (!WasSuccessful())
        return false;

      if (Result.Contains("[Bridge] game paused"))
        return true;

      return false;
    }

    protected override void OnFinish(RCONService owner)
    {
      return;
    }
  }

  public class TogglePauseCommand : L4D2CommandBase
  {
    public TogglePauseCommand() : base(ServerCommands.TogglePause)
    {
      Command = "sm_bridge_togglepause";
    }

    protected override void OnFinish(RCONService owner)
    {
      return;
    }
  }
}
