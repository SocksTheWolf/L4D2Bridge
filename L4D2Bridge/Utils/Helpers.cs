using L4D2Bridge.Settings;
using System;

namespace L4D2Bridge.Utils
{
  public static class Helpers
  {
    public static bool LongPrint = false;

    public static void SetSettings(DebugSettings settings)
    {
      LongPrint = settings.ShowExceptionDetails;
    }

    public static string PrintException(Exception ex)
    {
      return LongPrint ? ex.ToString()/*.Replace(System.Environment.NewLine, " ")*/ : ex.Message;
    }
  }
}
