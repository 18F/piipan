using Newtonsoft.Json;

namespace Piipan.Match.Orchestrator
{
    /// <summary>
    /// Represents the top-level response body for a successful API response
    /// </summary>
    public class OrchMatchResponse
    {
        [JsonProperty("data")]
        public OrchMatchResponseData Data { get; set; } = new OrchMatchResponseData();

        public string ToJson()
        {
        return Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
        }
    }
}
