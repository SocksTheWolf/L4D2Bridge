using CoreRCON;
using System;
using System.Threading.Tasks;
using L4D2Bridge.Types;
using L4D2Bridge.Utils;
using System.Collections.Generic;
using WeightedRandomLibrary;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;

namespace L4D2Bridge.Models
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
                Result = Result.Remove(RemoveCommandRepeat);

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
                Task.Run(async () => {
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

    public class SpawnMobCommand : L4D2CommandBase
    {
        private readonly int NumZombies = 0;
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

    public class HealAllPlayers : L4D2CommandBase
    {
        private readonly int HealthAmount;
        public HealAllPlayers(int InHealthAmount, string InSender) : base(ServerCommands.HealPlayers, InSender)
        {
            HealthAmount = InHealthAmount;
            Command = $"sm_bridge_healall {HealthAmount} {Sender}";
        }

        public override string ToString()
        {
            return base.ToString() + $", Amount[{HealthAmount}]";
        }

        protected override string GetSuccessString()
        {
            return "[Bridge] health healed";
        }
    }

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
    public static class L4D2CommandBuilder
    {
        private static Random rng = new();
        private static MobSizeSettings Mobs = new();

        // Negative Command Weight Randomization
        private static WeightedRandomizer<L4D2Action>? WeightedNegativeRandom = null;
        private static WeightedRandomizer<L4D2Action>? WeightedSpecialInfected = null;

        // All Command Randomization
        private readonly static L4D2Action[] PositiveActions;
        private readonly static L4D2Action[] NegativeActions;
        private readonly static L4D2Action[] SpawnInfectedActions;

        // Build the command lists from our existing enum using reflection.
        static L4D2CommandBuilder()
        {
            var AllEnumVals = typeof(L4D2Action).GetEnumValues().OfType<L4D2Action>();
            PositiveActions = AllEnumVals.Where(act => act.IsPositive()).ToArray();
            NegativeActions = AllEnumVals.Where(act => act.IsNegative()).ToArray();
            SpawnInfectedActions = AllEnumVals.Where(act => act.SpawnsSpecialInfected()).ToArray();
        }
        
        public static void Initialize(ConfigData config)
        {
            Mobs = config.MobSizes;
            if (config.NegativeActionWeights != null)
                InitializeWeightedRandoms(config.NegativeActionWeights);
        }

        public static void InitializeWeightedRandoms(Dictionary<L4D2Action, int> RandomWeighting)
        {
            // Create the negative randomization list.
            Option<L4D2Action>[] NegativeRandArray = RandomWeighting
                .Where(itm => NegativeActions.Contains(itm.Key))
                .Select(p => new Option<L4D2Action>(p.Key, p.Value)).ToArray();

            Option<L4D2Action>[] SpawnSpecialInfectedArray = RandomWeighting
                .Where(itm => SpawnInfectedActions.Contains(itm.Key))
                .Select(p => new Option<L4D2Action>(p.Key, p.Value)).ToArray();

            WeightedNegativeRandom = new WeightedRandomizer<L4D2Action>(NegativeRandArray);
            WeightedSpecialInfected = new WeightedRandomizer<L4D2Action>(SpawnSpecialInfectedArray);
        }

        public static L4D2CommandBase? BuildCommand(L4D2Action Action, string SenderName)
        {
            switch (Action)
            {
                default:
                case L4D2Action.None:
                    return null;
                case L4D2Action.SpawnTank:
                    return new SpawnZombieCommand("tank", SenderName);
                case L4D2Action.SpawnSpitter:
                    return new SpawnZombieCommand("spitter", SenderName);
                case L4D2Action.SpawnJockey:
                    return new SpawnZombieCommand("jockey", SenderName);
                case L4D2Action.SpawnWitch:
                    return new SpawnZombieCommand("witch", SenderName);
                case L4D2Action.SpawnBoomer:
                    return new SpawnZombieCommand("boomer", SenderName);
                case L4D2Action.SpawnHunter:
                    return new SpawnZombieCommand("hunter", SenderName);
                case L4D2Action.SpawnCharger:
                    return new SpawnZombieCommand("charger", SenderName);
                case L4D2Action.SpawnSmoker:
                    return new SpawnZombieCommand("smoker", SenderName);
                case L4D2Action.Lootbox:
                    return new SpawnLootboxCommand(SenderName);
                case L4D2Action.SupplyCrate:
                    return new SpawnSupplyCrateCommand(SenderName);
                case L4D2Action.SpawnMobMedium:
                    return new SpawnMobCommand(Mobs.Medium.GetSpawnAmount(ref rng), SenderName);
                case L4D2Action.SpawnMobSmall:
                    return new SpawnMobCommand(Mobs.Small.GetSpawnAmount(ref rng), SenderName);
                case L4D2Action.SpawnMobLarge:
                    return new SpawnMobCommand(Mobs.Large.GetSpawnAmount(ref rng), SenderName);
                case L4D2Action.SpawnMob:
                    return new SpawnMobCommand(Mobs.Rand.GetSpawnAmount(ref rng), SenderName);
                case L4D2Action.HealAllPlayersSmall:
                    return new HealAllPlayers(10, SenderName);
                case L4D2Action.HealAllPlayersLarge:
                    return new HealAllPlayers(50, SenderName);
                case L4D2Action.HealAllPlayersRand:
                    if (rng.NextBool())
                        return BuildCommand(L4D2Action.HealAllPlayersLarge, SenderName);
                    else
                        return BuildCommand(L4D2Action.HealAllPlayersSmall, SenderName);
                case L4D2Action.RespawnAllPlayers:
                    return new RespawnAllPlayers(SenderName);
                case L4D2Action.UppiesPlayers:
                    return new GetUpPlayers(SenderName);
                case L4D2Action.RandomSpecialInfected:
                    if (WeightedSpecialInfected != null)
                        return BuildCommand(WeightedSpecialInfected.Next().Value, SenderName);
                    else
                        return BuildCommand(SpawnInfectedActions[rng.Next(0, SpawnInfectedActions.Length)], SenderName);
                case L4D2Action.RandomPositive:
                    return BuildCommand(PositiveActions[rng.Next(0, PositiveActions.Length)], SenderName);
                case L4D2Action.RandomNegative:
                    if (WeightedNegativeRandom != null)
                        return BuildCommand(WeightedNegativeRandom.Next().Value, SenderName);
                    else
                        return BuildCommand(NegativeActions[rng.Next(0, NegativeActions.Length)], SenderName);
                case L4D2Action.Random:
                    if (rng.NextBool())
                        return BuildCommand(L4D2Action.RandomPositive, SenderName);
                    else
                        return BuildCommand(L4D2Action.RandomNegative, SenderName);
            }
        }
    }
}
