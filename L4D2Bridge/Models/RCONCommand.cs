using CoreRCON;
using System;
using System.Threading.Tasks;
using L4D2Bridge.Types;
using System.Collections.Generic;
using WeightedRandomLibrary;
using System.Linq;

namespace L4D2Bridge.Models
{
    // To add more commands, simply:
    // - create a class below that inherits from L4D2CommandBase
    // - create a new enum action in L4D2Actions
    // - add a ServerCommands enum entry
    // - add its corresponding class construction to L4D2CommandBuilder.InitializeRandoms
    // - Classify it as either a positive or negative command in the actions arrays

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
        public void Retry(RCONService owner)
        {
            if (!CanRetry)
                return;

            ++Attempts;
            HasRan = false;
            owner.PushToConsole($"Enqueueing {ToString()} for retry. Attempts {Attempts}");
            if (!RetriesImmediate)
            {
                // Boot up retrying this task again up to a minute later.
                Task.Run(async () => {
                    await Task.Delay(Math.Min(200 * (int)Math.Pow(2, Attempts) / 2, 60000));
                    owner.AddNewCommand(this);
                }).ConfigureAwait(false);
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
            RetriesImmediate = true;
        }

        // NOTE: This is not valid to call unless WasSuccessful() is true
        public bool IsPaused()
        {
            // TODO: Consider throwing if not successful
            if (!WasSuccessful())
                return false;

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

        public override string ToString()
        {
            return $"Command[{Enum.GetName(typeof(ServerCommands), Type)}], Args[{Command}]";
        }
    }

    // Static class to help build console commands based on a given action.
    static public class L4D2CommandBuilder
    {
        private static Random rng = new Random();
        public static MobSizeSettings Mobs = new MobSizeSettings();

        // Negative Command Weight Randomization
        private static WeightedRandomizer<L4D2Action>? WeightedNegativeRandom = null;

        // All Command Randomization
        private readonly static L4D2Action[] PositiveActions = { L4D2Action.Lootbox, L4D2Action.SupplyCrate };
        private readonly static L4D2Action[] NegativeActions = { L4D2Action.SpawnTank, L4D2Action.SpawnSpitter,
            L4D2Action.SpawnWitch, L4D2Action.SpawnTank, L4D2Action.SpawnSmoker, L4D2Action.SpawnHunter, L4D2Action.SpawnJockey, L4D2Action.SpawnBoomer,
            L4D2Action.SpawnCharger, L4D2Action.SpawnMob, L4D2Action.SpawnMobLarge, L4D2Action.SpawnMobMedium, L4D2Action.SpawnMobSmall };

        public static void Initialize(ConfigData config)
        {
            Mobs = config.MobSizes;
            if (config.NegativeActionWeights != null)
                InitializeWeightedRandoms(config.NegativeActionWeights);
        }

        public static void InitializeWeightedRandoms(Dictionary<L4D2Action, int> RandomWeighting)
        {
            // Create the negative randomization list.
            List<Option<L4D2Action>> NegativeRandList = RandomWeighting
                .Where(itm => NegativeActions.Contains(itm.Key))
                .Select(p => new Option<L4D2Action>(p.Key, p.Value)).ToList();

            WeightedNegativeRandom = new WeightedRandomizer<L4D2Action>(NegativeRandList);
        }

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
                case L4D2Action.RandomPositive:
                    outCommand = BuildCommand(PositiveActions[rng.Next(0, PositiveActions.Length)], SenderName); break;
                case L4D2Action.RandomNegative:
                    if (WeightedNegativeRandom != null)
                        outCommand = BuildCommand(WeightedNegativeRandom.Next().Value, SenderName);
                    else
                        outCommand = BuildCommand(NegativeActions[rng.Next(0, NegativeActions.Length)], SenderName);
                break;
                case L4D2Action.Random:
                    if (rng.Next(0, 2) == 1)
                        outCommand = BuildCommand(L4D2Action.RandomPositive, SenderName);
                    else
                        outCommand = BuildCommand(L4D2Action.RandomNegative, SenderName);
                break;
            }

            return outCommand;
        }
    }
}
