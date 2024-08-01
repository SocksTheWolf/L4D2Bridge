using CoreRCON;
using System;
using System.Threading.Tasks;

namespace L4D2Bridge.Models
{
    public enum ECommandType {
        None,
        Raw,
        CheckPause,
        TogglePause,
        SpawnMob,
        SpawnZombie,
        SpawnLootbox,
        SpawnSupplyCrate
    };

    public abstract class L4D2CommandBase
    {
        // Status flags and stats
        private bool HasRan = false;
        private bool Successful = false;
        private int Attempts = 0;
        private DateTime LastRan = DateTime.MinValue;

        // Types
        protected ECommandType Type = ECommandType.None;
        protected bool IsSpawner = false;

        // Base Shared Information
        protected string Sender = "";
        protected string Command = "";
        protected string Result = "";
        
        protected L4D2CommandBase(ECommandType InType, string InSender = "")
        {
            Type = InType;
            // TODO: Consider better sanitization system
            Sender = InSender.Replace(' ', '_');
        }

        // For spawn related functions, this checks to see if the command executed successfully
        protected virtual string GetSuccessString() => "";

        // For extra functionality that can be ran after an execution (like for printing!)
        protected virtual void OnFinish(RCONService owner)
        {
            owner.PushToConsole(Result);
        }

        public async Task<bool> Execute(RCONService owner, RCON connection)
        {
            // If we've ran this task before and it failed, try again in one minute.
            // This is a really jank way to do command delays.
            if (LastRan != DateTime.MinValue && (DateTime.Now - LastRan) <= TimeSpan.FromMinutes(1)) 
            {
                return false;
            }

            try
            {
                LastRan = DateTime.Now;
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
                owner.PushToConsole($"Failed to execute {ToString()}");
            }
            return false;
        }
        public void Retry(RCONService owner)
        {
            ++Attempts;
            HasRan = false;
            owner.PushToConsole($"Enqueueing {ToString()} for retry. Attempts {Attempts}");
            owner.AddNewCommand(this);
        }

        public override string ToString()
        {
            return $"Type[{nameof(Type)}], Sender[{Sender}]";
        }

        public ECommandType GetCommandType() => Type;

        // Status Flags
        public bool IsFinished() => HasRan;
        public bool WasSuccessful() => Successful;
    }

    public class SpawnMobCommand : L4D2CommandBase
    {
        private int NumZombies = 0;
        public SpawnMobCommand(int Amount, string InSender) : base(ECommandType.SpawnMob, InSender)
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

    public class SpawnZombieCommand : L4D2CommandBase
    {
        string ZombieType;
        public SpawnZombieCommand(string InType, string InSender) : base(ECommandType.SpawnZombie, InSender)
        {
            IsSpawner = true;
            ZombieType = InType.ToLower();
            Command = $"sm_bridge_spawnmob {ZombieType} {Sender}";
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

    public class SpawnLootboxCommand : L4D2CommandBase
    {
        public SpawnLootboxCommand(string InSender) : base(ECommandType.SpawnLootbox, InSender)
        {
            IsSpawner = true;
            Command = $"sm_bridge_spawnlootbox {Sender}";
        }

        protected override string GetSuccessString()
        {
            return "[Bridge] lootbox spawned";
        }
    }

    public class SpawnSupplyCrateCommand : L4D2CommandBase
    {
        public SpawnSupplyCrateCommand(string InSender) : base(ECommandType.SpawnSupplyCrate, InSender)
        {
            IsSpawner = true;
            Command = $"sm_bridge_supplycrate {Sender}";
        }

        protected override string GetSuccessString()
        {
            return "[Bridge] supplies spawned";
        }
    }

    public class CheckPauseCommand : L4D2CommandBase
    {
        public CheckPauseCommand() : base(ECommandType.CheckPause)
        {
            Command = "sm_bridge_checkpause";
        }

        // NOTE: This is not valid to call unless WasSuccessful() is true
        public bool IsPaused()
        {
            // TODO: Consider throwing if not successful

            if (Result == "[Bridge] game paused")
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
        public TogglePauseCommand() : base(ECommandType.TogglePause)
        {
            Command = "sm_bridge_togglepause";
        }

        protected override void OnFinish(RCONService owner)
        {
            return;
        }
    }

    public class RawCommand : L4D2CommandBase
    {
        public RawCommand(string InCommand) : base(ECommandType.Raw) 
        {
            Command = InCommand; 
        }
    }
}
