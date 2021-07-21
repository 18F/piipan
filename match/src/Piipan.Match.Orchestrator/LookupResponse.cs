using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Piipan.Match.Orchestrator
{
    public class LookupResponse
    {

        [JsonProperty("data")]
        public RequestPerson Data { get; set; }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
        }
    }
}
