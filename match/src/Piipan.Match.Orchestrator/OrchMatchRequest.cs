using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Piipan.Match.Shared;

namespace Piipan.Match.Orchestrator
{
    /// <summary>
    /// Represents the full API request from a client
    /// </summary>
    public class OrchMatchRequest
    {
        [JsonProperty("data", Required = Required.Always)]
        public List<RequestPerson> Data { get; set; } = new List<RequestPerson>();

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
        }
    }

    /// <summary>
    /// Represents each person in an API request
    /// </summary>
    public class RequestPerson
    {
        [JsonProperty("last", Required = Required.Always)]
        public string Last { get; set; }

        [JsonProperty("first", Required = Required.Always)]
        public string First { get; set; }

        [JsonProperty("middle", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(JsonConverters.NullConverter))]
        public string Middle { get; set; }

        [JsonProperty("dob", Required = Required.Always)]
        [JsonConverter(typeof(JsonConverters.DateTimeConverter))]
        public DateTime Dob { get; set; }

        [JsonProperty("ssn", Required = Required.Always)]
        public string Ssn { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.None);
        }
    }
}
