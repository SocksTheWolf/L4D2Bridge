using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace L4D2Bridge.Types
{
    // A series of actions that can be performed on a server as defined in action rule configurations.
    [JsonConverter(typeof(StringEnumConverter))]
    public enum L4D2Action
    {
        None,
        SpawnTank,
        SpawnSpitter,
        SpawnJockey,
        SpawnWitch,
        SpawnMob,
        SpawnMobSmall,
        SpawnMobMedium,
        SpawnMobLarge,
        SpawnBoomer,
        SpawnHunter,
        SpawnCharger,
        SpawnSmoker,
        Lootbox,
        SupplyCrate,
        RandomPositive,
        RandomNegative,
        Random
    }
}
