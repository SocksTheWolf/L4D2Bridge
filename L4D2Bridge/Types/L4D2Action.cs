using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace L4D2Bridge.Types
{
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
        SupplyCrate
    }
}
