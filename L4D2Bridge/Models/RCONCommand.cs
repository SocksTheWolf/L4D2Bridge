using CoreRCON;
using System;
using System.Threading.Tasks;
using L4D2Bridge.Types;

namespace L4D2Bridge.Models
{
    // To add more commands, simply:
    // - create a new enum in L4D2Actions
    // - add its corresponding class construction to L4D2CommandBuilder
    public abstract class L4D2CommandBase
    {
        // Status flags and stats
        private bool HasRan = false;
        private bool Successful = false;
        private int Attempts = 0;
        private DateTime LastRan = DateTime.MinValue;

        // Types
        protected ServerCommands Type = ServerCommands.None;
        protected bool IsSpawner = false;

        // Base Shared Information
        protected string Sender = "";
        protected string Command = "";
        protected string Result = "";
        
        protected L4D2CommandBase(ServerCommands InType, string InSender = "")
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

        public ServerCommands GetCommandType() => Type;

        // Status Flags
        public bool IsFinished() => HasRan;
        public bool WasSuccessful() => Successful;
    }

    public class SpawnMobCommand : L4D2CommandBase
    {
        private int NumZombies = 0;
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

    public class SpawnZombieCommand : L4D2CommandBase
    {
        string ZombieType;
        public SpawnZombieCommand(string InZombieType, string InSender) : base(ServerCommands.SpawnZombie, InSender)
        {
            IsSpawner = true;
            ZombieType = InZombieType.ToLower();
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

    public class CheckPauseCommand : L4D2CommandBase
    {
        public CheckPauseCommand() : base(ServerCommands.CheckPause)
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
        public TogglePauseCommand() : base(ServerCommands.TogglePause)
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
        public RawCommand(string InCommand) : base(ServerCommands.Raw) 
        {
            Command = InCommand; 
        }
    }

    // Static class to help build console commands based on a given action.
    static public class L4D2CommandBuilder
    {
        private static Random rng = new Random();
        public static MobSizeSettings Mobs = new MobSizeSettings();

        public static L4D2CommandBase? BuildCommand(L4D2Action Action, string SenderName)
        {
            L4D2CommandBase? outCommand = null;
            switch (Action)
            {
                default:
                case L4D2Action.None:
                    return null;
                case L4D2Action.SpawnTank:
                    outCommand = new SpawnZombieCommand("tank", SenderName); break;
                case L4D2Action.SpawnSpitter:
                    outCommand = new SpawnZombieCommand("spitter", SenderName); break;
                case L4D2Action.SpawnJockey:
                    outCommand = new SpawnZombieCommand("jockey", SenderName); break;
                case L4D2Action.SpawnWitch:
                    outCommand = new SpawnZombieCommand("witch", SenderName); break;
                case L4D2Action.SpawnBoomer:
                    outCommand = new SpawnZombieCommand("boomer", SenderName); break;
                case L4D2Action.SpawnHunter:
                    outCommand = new SpawnZombieCommand("hunter", SenderName); break;
                case L4D2Action.SpawnCharger:
                    outCommand = new SpawnZombieCommand("charger", SenderName); break;
                case L4D2Action.SpawnSmoker:
                    outCommand = new SpawnZombieCommand("smoker", SenderName); break;
                case L4D2Action.Lootbox:
                    outCommand = new SpawnLootboxCommand(SenderName); break;
                case L4D2Action.SupplyCrate:
                    outCommand = new SpawnSupplyCrateCommand(SenderName); break;
                case L4D2Action.SpawnMobMedium:
                    outCommand = new SpawnMobCommand(Mobs.Medium.GetSpawnAmount(ref rng), SenderName); break;
                case L4D2Action.SpawnMobSmall:
                    outCommand = new SpawnMobCommand(Mobs.Small.GetSpawnAmount(ref rng), SenderName); break;
                case L4D2Action.SpawnMobLarge:
                    outCommand = new SpawnMobCommand(Mobs.Large.GetSpawnAmount(ref rng), SenderName); break;
                case L4D2Action.SpawnMob:
                    outCommand = new SpawnMobCommand(Mobs.Rand.GetSpawnAmount(ref rng), SenderName); break;
            }

            return outCommand;
        }
    }
}
