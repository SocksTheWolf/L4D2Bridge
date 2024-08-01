using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace L4D2Bridge.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ConfigData
    {
        // Internals
        public bool IsValid { get; private set; } = false;

        // Statics
        public static string FileName = "config.json";

        /*** Connection Information ***/
        [JsonProperty]
        public string RConServerIP { get; set; } = string.Empty;

        [JsonProperty]
        public int RConServerPort { get; set; } = 27015;

        [JsonProperty]
        public string RConPassword { get; set; } = string.Empty;

        /*** Tiltify API Settings ***/
        [JsonProperty]
        public bool UseTiltify { get; set; } = true;

        [JsonProperty]
        public string TiltifyOAuthToken { get; set; } = string.Empty;

        [JsonProperty]
        public string TiltifyClientID { get; set; } = string.Empty;

        [JsonProperty]
        public string TiltifyClientSecret { get; set; } = string.Empty;

        [JsonProperty]
        public string? TiltifyRefreshToken { get; set; } = string.Empty;

        [JsonProperty]
        public string TiltifyCampaignID { get; set; } = string.Empty;

        // https://github.com/Tiltify/api/issues/9 (it's 5, and I will come after you if you limit me)
        [JsonProperty]
        public int TiltifyPollingInterval { get; set; } = 5;

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
                        List<string> checkIfNotNull = [configData.RConServerIP, configData.RConPassword];

                        // Add important Tiltify configs if we're using it.
                        if (configData.UseTiltify)
                            checkIfNotNull.AddRange([configData.TiltifyClientID, configData.TiltifyClientSecret, configData.TiltifyCampaignID]);

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
            string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
            using (StreamWriter FileWriter = File.CreateText(FileName))
            {
                FileWriter.WriteLine(jsonString);
            }
        }
    }
}
