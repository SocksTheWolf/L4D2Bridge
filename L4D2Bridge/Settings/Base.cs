using System.Collections.Generic;
using System.Linq;

namespace L4D2Bridge.Settings
{
  /*** Base Types/Classes ***/
  using RequiredFieldContainer = List<string>;
  public abstract class SettingsVerifier
  {
    public abstract void AddRequiredFields(ref RequiredFieldContainer RequiredFieldObj);
    public bool IsValid()
    {
      RequiredFieldContainer checkIfNotNull = [];
      AddRequiredFields(ref checkIfNotNull);

      if (checkIfNotNull.Any(it => string.IsNullOrEmpty(it)))
        return false;
      return true;
    }
  }
}
