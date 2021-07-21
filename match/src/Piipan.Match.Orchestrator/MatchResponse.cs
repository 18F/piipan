using System.Collections.Generic;
using Newtonsoft.Json;

namespace Piipan.Match.Orchestrator
{

    public class MatchResponse
    {
        [JsonProperty("matches")]
        public List<PiiRecord> Matches { get; set; }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
        }
    }
}
