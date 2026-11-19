using Newtonsoft.Json;
using System;

namespace L4D2Bridge.Settings
{
  [JsonObject(ItemRequired = Required.Always)]
  public class SpawnSizeRange(int min, int max)
  {
    [JsonProperty(PropertyName = "min")]
    public int Min = Math.Max(0, min);

    [JsonProperty(PropertyName = "max")]
    public int Max = max;

    public int GetSpawnAmount(ref Random rng)
    {
      return rng.Next(Min, Max+1);
    }
  }

  [JsonObject(MemberSerialization.OptOut)]
  public class MobSizeSettings
  {
    public SpawnSizeRange Small = new(3, 5);
    public SpawnSizeRange Medium = new(6, 12);
    public SpawnSizeRange Large = new(15, 25);
    public SpawnSizeRange Rand = new(4, 20);
  }
}
