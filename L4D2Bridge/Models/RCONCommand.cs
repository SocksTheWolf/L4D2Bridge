using System;
using L4D2Bridge.Types;
using L4D2Bridge.Utils;
using L4D2Bridge.Settings;
using L4D2Bridge.Commands;
using System.Collections.Generic;
using WeightedRandomLibrary;
using System.Linq;
using System.Linq.Dynamic.Core;

namespace L4D2Bridge.Models
{
  // Static class to help build console commands based on a given action.
  public static class L4D2CommandBuilder
  {
    private static Random rng = new Random(Guid.NewGuid().GetHashCode());
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
        case L4D2Action.SpawnMobRand:
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
        case L4D2Action.ExplosiveCars:
          return new ExplosiveCars(SenderName);
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
