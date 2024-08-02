using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace L4D2Bridge.Models
{
    using RequiredFieldContainer = List<string>;

    [JsonObject(MemberSerialization.OptOut)]
    public class TiltifySettings
    {
        [JsonProperty(Required = Required.Always)]
        public bool Enabled { get; set; } = true;

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

        public void AddRequiredFields(ref RequiredFieldContainer RequiredFieldObj)
        {
            if (Enabled)
                RequiredFieldObj.AddRange([ClientID, ClientSecret, CampaignID]);
        }
    }

    [JsonObject(MemberSerialization.OptOut, ItemRequired = Required.Always)]
    public class TwitchSettings
    {
        [JsonProperty]
        public bool Enabled { get; set; } = false;

        [JsonProperty]
        public string ChannelName { get; set; } = string.Empty;

        [JsonProperty]
        public string BotUserName { get; set; } = string.Empty;

        [JsonProperty]
        public string OAuthToken { get; set; } = string.Empty;

        public void AddRequiredFields(ref RequiredFieldContainer RequiredFieldObj)
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
        [JsonProperty]
        public string RConServerIP { get; set; } = string.Empty;

        [JsonProperty]
        public int RConServerPort { get; set; } = 27015;

        [JsonProperty]
        public string RConPassword { get; set; } = string.Empty;

        /*** Twitch Connection Information ***/
        [JsonProperty(PropertyName = "twitch", NullValueHandling = NullValueHandling.Include, Required=Required.AllowNull)]
        public TwitchSettings? TwitchSettings { get; set; }

        /*** Tiltify API Settings ***/
        [JsonProperty(PropertyName = "tiltify", NullValueHandling = NullValueHandling.Include, Required = Required.AllowNull)]
        public TiltifySettings? TiltifySettings { get; set; }

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

                        // Add important Tiltify configs if we're using it.
                        if (configData.TiltifySettings != null)
                            configData.TiltifySettings.AddRequiredFields(ref checkIfNotNull);

                        // Add important Twitch configs if we're using it.
                        if (configData.TwitchSettings != null)
                            configData.TwitchSettings.AddRequiredFields(ref checkIfNotNull);

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
