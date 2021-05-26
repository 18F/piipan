using System;
using System.Collections.Generic;

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
        public string? RecentBenefitMonths { get; set; }
    }
}
