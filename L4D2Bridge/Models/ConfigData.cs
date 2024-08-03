using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using L4D2Bridge.Types;

namespace L4D2Bridge.Models
{
    /*** Base Types/Classes ***/
    using RequiredFieldContainer = List<string>;
    public abstract class SettingsVerifier
    {
        public abstract void AddRequiredFields(ref RequiredFieldContainer RequiredFieldObj);
    }

    /*** Settings for Tiltify ***/
    [JsonObject(MemberSerialization.OptOut)]
    public class TiltifySettings : SettingsVerifier
    {
        [JsonProperty(Required = Required.Always)]
        public bool Enabled { get; set; } = false;

        [JsonProperty(Required = Required.Always)]
        public string OAuthToken { get; set; } = string.Empty;

        [JsonProperty(Required = Required.Always)]
        public string ClientID { get; set; } = string.Empty;

        [JsonProperty(Required = Required.Always)]
        public string ClientSecret { get; set; } = string.Empty;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? RefreshToken { get; set; } = null;

        [JsonProperty(Required = Required.Always)]
        public string CampaignID { get; set; } = string.Empty;

        // https://github.com/Tiltify/api/issues/9 (it's 5, and I will come after you if you limit me)
        [JsonProperty]
        public int PollingInterval { get; set; } = 5;

        public override void AddRequiredFields(ref RequiredFieldContainer RequiredFieldObj)
        {
            if (Enabled)
                RequiredFieldObj.AddRange([ClientID, ClientSecret, CampaignID]);
        }
    }

    /*** Settings for Twitch ***/
    [JsonObject(MemberSerialization.OptOut, ItemRequired = Required.Always)]
    public class TwitchSettings : SettingsVerifier
    {
        [JsonProperty]
        public bool Enabled { get; set; } = false;

        [JsonProperty]
        public string ChannelName { get; set; } = string.Empty;

        [JsonProperty]
        public string BotUserName { get; set; } = string.Empty;

        [JsonProperty]
        public string OAuthToken { get; set; } = string.Empty;

        public override void AddRequiredFields(ref RequiredFieldContainer RequiredFieldObj)
        {
            if (Enabled)
                RequiredFieldObj.AddRange([ChannelName, BotUserName, OAuthToken]);
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ConfigData
    {
        // Internals
        public bool IsValid { get; private set; } = false;

        // Statics
        public static string FileName = "config.json";

        /*** Server Connection Information ***/
        [JsonProperty(Required = Required.Always)]
        public string RConServerIP { get; set; } = string.Empty;

        [JsonProperty(Required = Required.Always)]
        public int RConServerPort { get; set; } = 27015;

        [JsonProperty]
        public string RConPassword { get; set; } = string.Empty;

        /*** Twitch Settings ***/
        [JsonProperty(PropertyName = "twitch")]
        public TwitchSettings TwitchSettings { get; set; } = new TwitchSettings();

        /*** Tiltify Settings ***/
        [JsonProperty(PropertyName = "tiltify")]
        public TiltifySettings TiltifySettings { get; set; } = new TiltifySettings();

        /*** Rules Settings ***/
        [JsonProperty(PropertyName = "actions", Required = Required.Always)]
        public Dictionary<string, List<L4D2Action>> Actions = new Dictionary<string, List<L4D2Action>>();

        // Maximum amount of times to retry a task
        [JsonProperty]
        public int MaxTaskRetries = 10;

        /*** Mob Settings ***/
        [JsonProperty(PropertyName = "mobsizes")]
        public MobSizeSettings MobSizes { get; set; } = new MobSizeSettings();

        /*** UI Settings ***/

        /*** Config Loading/Saving ***/
        public static ConfigData LoadConfigData()
        {
            ConfigData configData = new ConfigData();
            if (!File.Exists(FileName))
            {
                configData.SaveConfigData();
                return configData;
            }

            string json = File.ReadAllText(FileName);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var outputConfig = JsonConvert.DeserializeObject<ConfigData>(json);
                    if (outputConfig != null)
                    {
                        configData = outputConfig;
                        configData.IsValid = true;
                        RequiredFieldContainer checkIfNotNull = [configData.RConServerIP, configData.RConPassword];

                        // Get all of our properties in the config class of type SettingsVerifier
                        var verifyProperties = configData.GetType().GetProperties().Where(prop => prop.PropertyType.IsSubclassOf(typeof(SettingsVerifier)));
                        foreach (PropertyInfo? property in verifyProperties)
                        {
                            // Attempt to get the value of the property if it is set
                            object? Value = property?.GetValue(configData);
                            if (Value != null)
                            {
                                // We have the object, so cast it to the SettingsVerifier class and add the required fields
                                ((SettingsVerifier)Value).AddRequiredFields(ref checkIfNotNull);
                            }
                        }

                        // Check if any of the settings are invalid.
                        if (checkIfNotNull.Any(it => string.IsNullOrEmpty(it)))
                            configData.IsValid = false;
                        
                        Console.WriteLine("Settings loaded");
                    }
                }
                catch
                {
                    Console.WriteLine("Failed to load settings");
                    configData.IsValid = false;
                }
            }
            
            return configData;
        }

        public void SaveConfigData()
        {
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
