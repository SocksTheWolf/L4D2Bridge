using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using L4D2Bridge.Types;
using L4D2Bridge.Settings;

namespace L4D2Bridge.Models
{
  using RequiredFieldContainer = List<string>;

  [JsonObject(MemberSerialization.OptIn)]
  public class ConfigData
  {
    // Internals
    public bool IsValid { get; private set; } = false;

    // Statics
    private static readonly string FileName = "config.json";

    /*** Server Connection Information ***/
    [JsonProperty(PropertyName = "server")]
    public ServerSettings ServerSettings { get; set; } = new ServerSettings();

    /*** Twitch Settings ***/
    [JsonProperty(PropertyName = "twitch")]
    public TwitchSettings TwitchSettings { get; set; } = new TwitchSettings();

    /*** Tiltify Settings ***/
    [JsonProperty(PropertyName = "tiltify")]
    public TiltifySettings TiltifySettings { get; set; } = new TiltifySettings();

    [JsonProperty(PropertyName = "poll")]
    public RequestPollSettings PollSettings { get; set; } = new RequestPollSettings();

    /*** Rules Settings ***/
    [JsonProperty(Required = Required.Always)]
    public Dictionary<string, List<L4D2Action>> Actions = [];

    /*** Mob Settings ***/
    [JsonProperty]
    public MobSizeSettings MobSizes { get; set; } = new MobSizeSettings();

    /*** Negative Command Randomization ***/
    [JsonProperty(PropertyName = "NegativeWeights")]
    public Dictionary<L4D2Action, int>? NegativeActionWeights { get; set; }

    /*** UI Settings ***/
    [JsonProperty]
    public int MaxMessageLifetime = 5;

    [JsonProperty]
    public int MaxInputHistory = 5;

    /*** Testing ***/
    [JsonProperty(PropertyName = "test")]
    public TestSettings TestSettings { get; set; } = new TestSettings();

    /*** Debug ***/
    [JsonProperty(PropertyName = "Debug")]
    public DebugSettings DebugSettings { get; set; } = new DebugSettings();

    /*** Utils ***/
    public bool IsUsingTwitch() => TwitchSettings != null && TwitchSettings.Enabled;
    public bool IsUsingRequestPoller() => PollSettings != null && PollSettings.Enabled;
    public bool IsUsingTiltify() => TiltifySettings != null && TiltifySettings.Enabled;
    public bool IsUsingTest() => TestSettings != null && TestSettings.Enabled;

    /*** Config Loading/Saving ***/
    public static ConfigData? LoadConfigData()
    {
      if (!File.Exists(FileName))
      {
        ConfigData configData = new();
        configData.SaveConfigData(true);
        return configData;
      }

      string json = File.ReadAllText(FileName);
      if (!string.IsNullOrWhiteSpace(json))
      {
        try
        {
          var outputConfig = JsonConvert.DeserializeObject<ConfigData>(json);
          if (outputConfig != null)
          {
            RequiredFieldContainer checkIfNotNull = [];

            // Get all of our properties in the config class of type SettingsVerifier
            var verifyProperties = outputConfig.GetType().GetProperties().Where(prop => prop.PropertyType.IsSubclassOf(typeof(SettingsVerifier)));
            foreach (PropertyInfo? property in verifyProperties)
            {
              // Attempt to get the value of the property if it is set
              object? Value = property?.GetValue(outputConfig);

              // We have the object, so cast it to the SettingsVerifier class and add the required fields
              if (Value != null)
              {
                ((SettingsVerifier)Value).AddRequiredFields(ref checkIfNotNull);
              }
            }

            // Check if any of the settings are invalid.
            if (checkIfNotNull.Any(it => string.IsNullOrEmpty(it)))
              outputConfig.IsValid = false;
            else
              outputConfig.IsValid = true;

            Console.WriteLine("Settings loaded");
            return outputConfig;
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine($"Failed to load settings {ex}");
        }
      }

      return null;
    }

    public void SaveConfigData(bool OverrideInvalid = false)
    {
      // If we are not valid, do not allow saving, unless override Invalid is true
      if (!IsValid && !OverrideInvalid)
        return;

      string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings
      {
        NullValueHandling = NullValueHandling.Include
      });
      using (StreamWriter FileWriter = File.CreateText(FileName))
      {
        FileWriter.WriteLine(jsonString);
      }
    }
  }
}
