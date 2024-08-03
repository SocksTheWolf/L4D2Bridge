using Newtonsoft.Json;
using System;

namespace L4D2Bridge.Types
{
    [JsonObject(ItemRequired = Required.Always)]
    public class SpawnSizeRange
    {
        [JsonProperty(PropertyName = "min")]
        public int Min;

        [JsonProperty(PropertyName = "max")]
        public int Max;

        public SpawnSizeRange(int min, int max)
        {
            Min = min; Max = max;
        }

        public int GetSpawnAmount(ref Random rng)
        {
            return rng.Next(Min, Max);
        }
    }

    [JsonObject(MemberSerialization.OptOut)]
    public class MobSizeSettings
    {
        public SpawnSizeRange Small = new SpawnSizeRange(3, 5);
        public SpawnSizeRange Medium = new SpawnSizeRange(6, 12);
        public SpawnSizeRange Large = new SpawnSizeRange(15, 25);
        public SpawnSizeRange Rand = new SpawnSizeRange(4, 20);
    }
}
