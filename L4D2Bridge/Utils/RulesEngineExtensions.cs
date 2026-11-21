using System.Linq;
using System;
using L4D2Bridge.Types;

namespace L4D2Bridge.Utils
{
  // Rules Engine Utilities
  public class REUtils
  {
    private static readonly Random rng = new(Guid.NewGuid().GetHashCode());

    // Returns if the string has a value and is not null, empty or whitespace
    public static bool HasValue(string input) => !string.IsNullOrWhiteSpace(input);

    // Takes either a CSV of values to compare against or will take a non-listed value
    public static bool CheckContains(string input, string valueOrCSV)
    {
      if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(valueOrCSV))
        return false;

      string checkLower = input.ToLower();
      // Check to see if this is a CSV list
      if (valueOrCSV.Contains(','))
      {
        var list = valueOrCSV.Split(',').ToList();
        return list.Any(item => checkLower.Contains(item.ToLower()));
      }
      else
      {
        return checkLower.Contains(valueOrCSV);
      }
    }

    public static bool PercentChance(int chance)
    {
      if (chance <= 0) return false;
      if (chance >= 100) return true;

      int rngRoll = rng.Next(0, 101);
      return rngRoll <= chance;
    }

    public static bool IsType(SourceEvent sev, string type) {
      string searchingFor = type.ToLower();
      string eventType = Enum.GetName(typeof(SourceEventType), sev.Type) ?? "";
      if (eventType != "" && searchingFor == eventType.ToLower())
        return true;

      eventType = sev.Type.GetEnumAttribute<SourceEventType, SEInfo>()?.Alias ?? "";
      if (eventType != "" && searchingFor == eventType.ToLower())
        return true;

      return false;
    }
  }
}
