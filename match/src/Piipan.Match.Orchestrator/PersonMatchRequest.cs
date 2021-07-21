using System;
using Newtonsoft.Json;
using Piipan.Match.Shared;

namespace Piipan.Match.Orchestrator
{
    public class PersonMatchRequest
    {
        [JsonProperty("query", Required = Required.Always)]
        public PersonMatchQuery Query { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
        }
    }

    public class PersonMatchQuery
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
