using System;
using System.Collections.Generic;
using CsvHelper.Configuration.Attributes;

#nullable enable

namespace Piipan.Etl
{
    /// <summary>
    /// A single record extracted from a CSV file formatted in accordance with
    /// <c>/etl/docs/csv/import-schema.json</c>.
    /// </summary>
    /// <remarks>
    /// PiiRecordMap ensures required fields are non-null and optional fields are
    /// are set to null.
    /// </remarks>
    public class PiiRecord
    {

        public string Last { get; set; } = null!;
        public string? First { get; set; }
        public string? Middle { get; set; }
        public DateTime Dob { get; set; }
        public string Ssn { get; set; } = null!;
        public string CaseId { get; set; } = null!;
        public string? ParticipantId { get; set; }
        public string? Exception { get; set; }
        public DateTime? BenefitsEndDate { get; set; }
        public List<DateTime> RecentBenefitMonths { get; set; } = new List<DateTime>();
        // Set Boolean values here, based on:
        // https://joshclose.github.io/CsvHelper/examples/configuration/attributes/
        // Values should mimic what is set in the Bulk Upload import schema
        [BooleanTrueValues("true")]
        [BooleanFalseValues("false")]
        public bool? ProtectLocation { get; set; }
    }
}
