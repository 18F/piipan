using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Piipan.Match.Shared;

namespace Piipan.Match.State
{

    public class MatchQueryResponse
    {
        [JsonProperty("matches")]
        public List<PiiRecord> Matches { get; set; }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
        }
    }

    public class PiiRecord
    {
        public PiiRecord()
        {
            State = Environment.GetEnvironmentVariable("StateAbbr");
        }

        [JsonProperty("last")]
        public string Last { get; set; }

        [JsonProperty("first")]
        public string First { get; set; }

        [JsonProperty("middle")]
        public string Middle { get; set; }

        [JsonProperty("ssn")]
        public string Ssn { get; set; }

        [JsonProperty("dob")]
        [JsonConverter(typeof(JsonConverters.DateTimeConverter))]
        public DateTime Dob { get; set; }

        [JsonProperty("exception")]
        public string Exception { get; set; }

        // Read-only
        [JsonProperty("state")]
        public string State { get; }

        // Read-only
        // Deprecated
        [JsonProperty("state_abbr")]
        public string StateAbbr
        {
            get
            {
                return State;
            }
        }

        [JsonProperty("case_id")]
        public string CaseId { get; set; }

        [JsonProperty("participant_id")]
        public string ParticipantId { get; set; }

        [JsonProperty("benefits_end_month")]
        [JsonConverter(typeof(JsonConverters.MonthEndConverter))]
        public DateTime? BenefitsEndMonth { get; set; }

        [JsonProperty("recent_benefit_months")]
        [JsonConverter(typeof(JsonConverters.MonthEndArrayConverter))]
        public List<DateTime> RecentBenefitMonths { get; set; } = new List<DateTime>();

        [JsonProperty("protect_location")]
        public bool? ProtectLocation { get; set; }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
        }
    }
}
