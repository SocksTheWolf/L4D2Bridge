using Newtonsoft.Json;

namespace L4D2Bridge.Types
{
    [JsonObject(MemberSerialization.OptIn, ItemRequired = Required.Default)]
    public class TwitchAuthData
    {
        [JsonProperty(PropertyName = "client_id")]
        public string ClientID { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "access_token")]
        public string OAuthToken { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "user_id")]
        public string UserID { get; set; } = string.Empty;

        [JsonProperty(PropertyName = "login")]
        public string Login { get; set; } = string.Empty;
    }
}
