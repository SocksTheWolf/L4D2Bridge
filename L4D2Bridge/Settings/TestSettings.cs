using L4D2Bridge.Models;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace L4D2Bridge.Settings
{
  using RequiredFieldContainer = List<string>;

  /*** Settings for Test Service ***/
  [JsonObject(MemberSerialization.OptOut, ItemRequired = Required.Always)]
  public class TestSettings : SettingsVerifier
  {
    public bool Enabled { get; set; } = false;
    public string WorkflowName { get; set; } = string.Empty;
    public int MinSecondsToWait { get; set; } = 1000;
    public int MaxMinutesToWait { get; set; } = 1;
    public double MaxSimulatedAmount { get; set; } = 100.00;

    public override void AddRequiredFields(ref RequiredFieldContainer RequiredFieldObj)
    {
      if (Enabled)
      {
        RequiredFieldObj.Add(WorkflowName);
      }
    }
  }
}
