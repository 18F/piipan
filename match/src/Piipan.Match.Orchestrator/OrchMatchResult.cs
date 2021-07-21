using System.Collections.Generic;
using Newtonsoft.Json;

namespace Piipan.Match.Orchestrator
{

    /// <summary>
    /// Represents the entire result for each person from an API request
    /// <para> Collects all matches from all states.</para>
    /// <para> Adds more properties than the generic responses from the per-state API's.</para>
    /// </summary>
    public class OrchMatchResult
    {
        [JsonProperty("index")]
        public int Index { get; set; }

        [JsonProperty("lookup_id")]
        public string LookupId { get; set; }

        [JsonProperty("matches")]
        public List<PiiRecord> Matches { get; set; }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
        }
    }
}
