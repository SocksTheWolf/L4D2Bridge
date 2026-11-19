using System;
using CoreRCON;
using System.Threading;
using System.Threading.Tasks;
using L4D2Bridge.Types;
using L4D2Bridge.Models;

namespace L4D2Bridge.Commands
{
  // To add more commands, simply:
  // - create a class below that inherits from L4D2CommandBase
  // - create a new enum action in L4D2Actions, adding the item with a display name + positive/negative grouping
  // - add a ServerCommands enum entry
  // - add its corresponding class construction to L4D2CommandBuilder.BuildCommand

  public abstract class L4D2CommandBase
  {
    // Status flags and stats
    private bool HasRan = false;
    private bool Successful = false;
    private int Attempts = 0;

    // Types and behavior settings
    protected ServerCommands Type = ServerCommands.None;
    protected bool IsSpawner = false;
    protected bool RetriesImmediate = false;
    protected bool CanRetry = true;

    // Base Shared Information
    protected string Sender;
    protected string Command = "";
    protected string Result = "";

    protected L4D2CommandBase(ServerCommands InType, string InSender = "")
    {
      Type = InType;
      if (!string.IsNullOrWhiteSpace(InSender))
      {
        char[] charArray = InSender.ToCharArray();

        // Sanitize the input of the sender name
        charArray = Array.FindAll<char>(charArray, (c => (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))));
        Sender = new string(charArray, 0, charArray.Length > 100 ? 100 : charArray.Length);
      }
      else
        Sender = "";
    }

    // For spawn related functions, this checks to see if the command executed successfully
    protected virtual string GetSuccessString() => "";

    // For extra functionality that can be ran after an execution (like for printing!)
    protected virtual void OnFinish(RCONService owner)
    {
      if (IsSpawner)
        return;

      // Remove RCON repeat signature, to hide ip addresses
      int RemoveCommandRepeat = Result.IndexOf("\nL ");
      if (RemoveCommandRepeat != -1)
        Result = Result[..RemoveCommandRepeat];

      owner.PushToConsole(Result);
    }

    public async Task<bool> Execute(RCONService owner, RCON connection)
    {
      try
      {
        Result = await connection.SendCommandAsync(Command);
        HasRan = true;
        if (IsSpawner)
        {
          if (Result.Contains(GetSuccessString()))
          {
            Successful = true;
          }
        }
        else
        {
          Successful = true;
        }

        if (Successful)
        {
          OnFinish(owner);
          return true;
        }
      }
      catch
      {
        owner.PushToConsole($"Failed to execute {this}");
      }
      return false;
    }
    public void Retry(RCONService owner, CancellationToken token)
    {
      if (!CanRetry)
        return;

      ++Attempts;
      HasRan = false;
      owner.PushToConsole($"Enqueueing {ToString()} for retry. Attempts {Attempts}");
      if (!RetriesImmediate)
      {
        // Boot up retrying this task again up to a minute later.
        Task.Run(async () =>
        {
          await Task.Delay(Math.Min(1000 * (int)Math.Pow(2, Attempts) / 2, 120000), token);
          owner.AddNewCommand(this);
        }, token).ConfigureAwait(false);
      }
      else
        owner.AddNewCommand(this);
    }

    public override string ToString()
    {
      return $"Command[{Enum.GetName(typeof(ServerCommands), Type)}], Sender[{Sender}]";
    }

    public ServerCommands GetCommandType() => Type;

    // Status Flags
    public bool IsFinished() => HasRan;
    public bool WasSuccessful() => Successful;
    public int GetAttemptCount() => Attempts;
  }
}
