using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Piipan.QueryTool
{
    public class MatchRequest
    {
        [JsonPropertyName("query")]
        public PiiRecord Query { get; set; }
    }

    public class PiiRecord : IQueryable
    {
        [Required]
        [Display(Name = "First name")]
        [JsonPropertyName("first")]
        public string FirstName { get; set; }

        [Display(Name = "Middle name")]
        [JsonPropertyName("middle")]
        public string MiddleName { get; set; }

        [Required]
        [Display(Name = "Last name")]
        [JsonPropertyName("last")]
        public string LastName { get; set; }

        [Required]
        [Display(Name = "Date of birth")]
        [DataType(DataType.Date),
            DisplayFormat(DataFormatString = "{0:MM/dd/yyyy}")]
        [JsonPropertyName("dob")]
        public DateTime? DateOfBirth { get; set; }

        [Required]
        [RegularExpression(@"^\d{3}-\d{2}-\d{4}$",
            ErrorMessage = "SSN must have the form 000-00-0000")]
        [Display(Name = "SSN")]
        [JsonPropertyName("ssn")]
        public string SocialSecurityNum { get; set; }

        [Display(Name = "StateName")]
        [JsonPropertyName("state_name")]
        public string StateName { get; set; }

        [Display(Name = "StateAbbr")]
        [JsonPropertyName("state_abbr")]
        public string StateAbbr { get; set; }

        [Display(Name = "CaseId")]
        [JsonPropertyName("case_id")]
        public string CaseId { get; set; }

        [Display(Name = "ParticipantId")]
        [JsonPropertyName("participant_id")]
        public string ParticipantId { get; set; }

        [Display(Name = "Lookup ID")]
        [JsonPropertyName("lookup_id")]
        public string LookupId { get; set; }
    }
}
