using System.Collections.Generic;
using Newtonsoft.Json;

namespace Piipan.Match.Orchestrator
{
    /// <summary>
    /// Represents the top-level data object in a successful API response
    /// </summary>
    public class OrchMatchResponseData
    {
        [JsonProperty("results")]
        public List<OrchMatchResult> Results { get; set; } = new List<OrchMatchResult>();

        [JsonProperty("errors")]
        public List<OrchMatchError> Errors { get; set; } = new List<OrchMatchError>();
    }
}
