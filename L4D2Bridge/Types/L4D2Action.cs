using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using L4D2Bridge.Utils;
using System;

namespace L4D2Bridge.Types
{
    // A series of actions that can be performed on a server as defined in action rule configurations.
    [JsonConverter(typeof(StringEnumConverter))]
    public enum L4D2Action
    {
        [RuleAction(ReadableName = "None")]
        None,
        [RuleAction(ReadableName = "Tank", NegativeEffect = true, SpawnsSpecialInfected = true)]
        SpawnTank,
        [RuleAction(ReadableName = "Spitter", NegativeEffect = true, SpawnsSpecialInfected = true)]
        SpawnSpitter,
        [RuleAction(ReadableName = "Jockey", NegativeEffect = true, SpawnsSpecialInfected = true)]
        SpawnJockey,
        [RuleAction(ReadableName = "Witch", NegativeEffect = true, SpawnsSpecialInfected = true)]
        SpawnWitch,
        [RuleAction(ReadableName = "Random Mob", NegativeEffect = true)]
        SpawnMob,
        [RuleAction(ReadableName = "Small Mob", NegativeEffect = true)]
        SpawnMobSmall,
        [RuleAction(ReadableName = "Medium Mob", NegativeEffect = true)]
        SpawnMobMedium,
        [RuleAction(ReadableName = "Large Mob", NegativeEffect = true)]
        SpawnMobLarge,
        [RuleAction(ReadableName = "Boomer", NegativeEffect = true, SpawnsSpecialInfected = true)]
        SpawnBoomer,
        [RuleAction(ReadableName = "Hunter", NegativeEffect = true, SpawnsSpecialInfected = true)]
        SpawnHunter,
        [RuleAction(ReadableName = "Charger", NegativeEffect = true, SpawnsSpecialInfected = true)]
        SpawnCharger,
        [RuleAction(ReadableName = "Smoker", NegativeEffect = true, SpawnsSpecialInfected = true)]
        SpawnSmoker,
        [RuleAction(ReadableName = "Lootbox", PositiveEffect = true)]
        Lootbox,
        [RuleAction(ReadableName = "Supplycrate", PositiveEffect = true)]
        SupplyCrate,
        [RuleAction(ReadableName = "Positive RNG")]
        RandomPositive,
        [RuleAction(ReadableName = "Negative RNG")]
        RandomNegative,
        [RuleAction(ReadableName = "Random Event")]
        Random,
        [RuleAction(ReadableName = "Random Special Infected")]
        RandomSpecialInfected,
        [RuleAction(ReadableName = "Heal All Small", PositiveEffect = true)]
        HealAllPlayersSmall,
        [RuleAction(ReadableName = "Heal All Large", PositiveEffect = true)]
        HealAllPlayersLarge,
        [RuleAction(ReadableName = "Respawned Players", PositiveEffect = true)]
        RespawnAllPlayers,
        [RuleAction(ReadableName = "Gave Players Uppies", PositiveEffect = true)]
        UppiesPlayers
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class RuleAction : Attribute
    {
        // A human readable name that can be printed to somewhere like a chat console
        public string ReadableName = string.Empty;
        // A flag to signify that this action is considered positive to players
        public bool PositiveEffect = false;
        // A flag to signify that this action is considered a hinderance
        public bool NegativeEffect = false;
        // A flag to signify that this involves spawning a special infected
        public bool SpawnsSpecialInfected = false;
    }

    // This extends onto the RuleAction above, to allow for pulling names and statuses easily
    public static class RuleActionHelper
    {
        public static string? GetReadableName(this L4D2Action Enum) => Enum.GetEnumAttribute<L4D2Action, RuleAction>()?.ReadableName;
        public static bool IsPositive(this L4D2Action Enum)
        {
            bool? ret = Enum.GetEnumAttribute<L4D2Action, RuleAction>()?.PositiveEffect;
            return (ret != null) && (bool)ret;
        }

        public static bool IsNegative(this L4D2Action Enum)
        {
            bool? ret = Enum.GetEnumAttribute<L4D2Action, RuleAction>()?.NegativeEffect;
            return (ret != null) && (bool)ret;
        }

        public static bool SpawnsSpecialInfected(this L4D2Action Enum)
        {
            bool? ret = Enum.GetEnumAttribute<L4D2Action, RuleAction>()?.SpawnsSpecialInfected;
            return (ret != null) && (bool)ret;
        }
    }
}
