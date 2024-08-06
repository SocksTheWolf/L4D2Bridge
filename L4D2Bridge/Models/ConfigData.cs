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
    [JsonObject(MemberSerialization.OptOut, ItemRequired = Required.Always)]
    public class TiltifySettings : SettingsVerifier
    {
        public bool Enabled { get; set; } = false;
        public string OAuthToken { get; set; } = string.Empty;
        public string ClientID { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, Required = Required.Default)]
        public string? RefreshToken { get; set; } = null;

        public string CampaignID { get; set; } = string.Empty;

        // https://github.com/Tiltify/api/issues/9 (it's 5, and I will come after you if you limit me)
        public int PollingInterval { get; set; } = 5;

        public override void AddRequiredFields(ref RequiredFieldContainer RequiredFieldObj)
        {
            if (Enabled)
                RequiredFieldObj.AddRange([ClientID, ClientSecret, CampaignID]);
        }
    }

    /*** Settings for Twitch ***/
    [JsonObject(MemberSerialization.OptOut, ItemRequired = Required.Always)]
    public class TwitchEvents
    {
        public bool OnCommand { get; set; } = false;
        public bool OnRaid { get; set; } = false;
        public bool OnSubscription { get; set; } = false;
        public bool OnResubscription { get; set; } = false;
        public bool OnGiftSubscription { get; set; } = false;
        public bool OnMultiGiftSubscription { get; set; } = false;
    }

    [JsonObject(MemberSerialization.OptOut, ItemRequired = Required.Always)]
    public class TwitchSettings : SettingsVerifier
    {
        public bool Enabled { get; set; } = false;
        public string[]? Channels { get; set; } = null;
        public string BotUserName { get; set; } = string.Empty;
        public string OAuthToken { get; set; } = string.Empty;
        // If the resulting actions from twitch events should be redirected to chat as well.
        public bool SendActionsToChat { get; set; } = false;
        // Whether to message when Tiltify events should message into the chat console
        public bool MessageOnTiltifyDonations { get; set; } = false;
        public int ChatCommandPercentChance { get; set; } = 50;
        public TwitchEvents Events { get; set; } = new TwitchEvents();

        public override void AddRequiredFields(ref RequiredFieldContainer RequiredFieldObj)
        {
            if (Enabled)
            {
                if (Channels != null)
                    RequiredFieldObj.AddRange(Channels);

                RequiredFieldObj.AddRange([BotUserName, OAuthToken]);
            }
        }
    }

    /*** Settings for a server ***/
    [JsonObject(MemberSerialization.OptOut, ItemRequired = Required.Always)]
    public class ServerSettings : SettingsVerifier
    {
        [JsonProperty(Required = Required.Always)]
        public string ServerIP { get; set; } = string.Empty;

        [JsonProperty(Required = Required.Always)]
        public int ServerPort { get; set; } = 27015;

        [JsonProperty(Required = Required.Always)]
        public string Password { get; set; } = string.Empty;

        // Maximum amount of times to retry a task
        [JsonProperty]
        public int MaxCommandAttempts = 10;

        public override void AddRequiredFields(ref RequiredFieldContainer RequiredFieldObj)
        {
            RequiredFieldObj.AddRange([ServerIP, Password]);
        }
    }

    /*** Settings for Test Service ***/
    [JsonObject(MemberSerialization.OptOut, ItemRequired = Required.Always)]
    public class TestSettings : SettingsVerifier
    {
        public bool Enabled { get; set; } = false;
        public string WorkflowName { get; set; } = string.Empty;
        public int MaxMinutesToWait { get; set; } = 1;

        public override void AddRequiredFields(ref RequiredFieldContainer RequiredFieldObj)
        {
            if (Enabled)
            {
                RequiredFieldObj.Add(WorkflowName);
            }
        }
    }

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

        /*** Testing ***/
        [JsonProperty(PropertyName = "test")]
        public TestSettings TestSettings { get; set; } = new TestSettings();

        /*** Utils ***/
        public bool IsUsingTwitch() => TwitchSettings != null && TwitchSettings.Enabled;
        public bool IsUsingTiltify() => TiltifySettings != null && TiltifySettings.Enabled;
        public bool IsUsingTest() => TestSettings != null && TestSettings.Enabled;

        /*** Config Loading/Saving ***/
        public static ConfigData LoadConfigData()
        {
            ConfigData configData = new();
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
                        RequiredFieldContainer checkIfNotNull = [];

                        // Get all of our properties in the config class of type SettingsVerifier
                        var verifyProperties = configData.GetType().GetProperties().Where(prop => prop.PropertyType.IsSubclassOf(typeof(SettingsVerifier)));
                        foreach (PropertyInfo? property in verifyProperties)
                        {
                            // Attempt to get the value of the property if it is set
                            object? Value = property?.GetValue(configData);

                            // We have the object, so cast it to the SettingsVerifier class and add the required fields
                            if (Value != null)
                                ((SettingsVerifier)Value).AddRequiredFields(ref checkIfNotNull);
                        }

                        // Check if any of the settings are invalid.
                        if (checkIfNotNull.Any(it => string.IsNullOrEmpty(it)))
                            configData.IsValid = false;
                        
                        Console.WriteLine("Settings loaded");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load settings {ex}");
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
