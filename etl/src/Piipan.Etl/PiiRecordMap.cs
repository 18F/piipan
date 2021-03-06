using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper.Configuration;
using Piipan.Shared.Helpers;

namespace Piipan.Etl
{
    /// <summary>
    /// Maps and validates a record from a CSV file formatted in accordance with
    /// <c>/etl/docs/csv/import-schema.json</c> to a <c>PiiRecord</c>.
    /// </summary>
    public class PiiRecordMap : ClassMap<PiiRecord>
    {
        public PiiRecordMap()
        {
            Map(m => m.Last).Name("last").Validate(field =>
            {
                return !string.IsNullOrEmpty(field.Field);
            });

            Map(m => m.First).Name("first")
                .TypeConverterOption.NullValues(string.Empty).Optional();

            Map(m => m.Middle).Name("middle")
                .TypeConverterOption.NullValues(string.Empty).Optional();

            Map(m => m.Dob).Name("dob");

            Map(m => m.Ssn).Name("ssn").Validate(field =>
            {
                Match match = Regex.Match(field.Field, "^[0-9]{3}-[0-9]{2}-[0-9]{4}$");
                return match.Success;
            });

            Map(m => m.Exception).Name("exception")
                .TypeConverterOption.NullValues(string.Empty).Optional();

            Map(m => m.CaseId).Name("case_id").Validate(field =>
            {
                return !string.IsNullOrEmpty(field.Field);
            });

            Map(m => m.ParticipantId).Name("participant_id")
                .TypeConverterOption.NullValues(string.Empty).Optional();

            Map(m => m.BenefitsEndDate)
                .Name("benefits_end_month")
                .Validate(field => {
                  if (String.IsNullOrEmpty(field.Field)) return true;

                  string[] formats={"yyyy-MM", "yyyy-M"};
                  DateTime dateValue;
                    var result = DateTime.TryParseExact(
                      field.Field,
                      formats,
                      new CultureInfo("en-US"),
                      DateTimeStyles.None,
                      out dateValue);
                    if (!result) return false;
                  return true;
                })
                .TypeConverter<ToMonthEndConverter>().Optional();

            Map(m => m.RecentBenefitMonths)
                .Name("recent_benefit_months")
                .Validate(field => {
                  if (String.IsNullOrEmpty(field.Field)) return true;

                  string[] formats={"yyyy-MM", "yyyy-M"};
                  string[] dates = field.Field.Split(' ');
                  foreach (string date in dates)
                  {
                    DateTime dateValue;
                    var result = DateTime.TryParseExact(
                      date,
                      formats,
                      new CultureInfo("en-US"),
                      DateTimeStyles.None,
                      out dateValue);
                    if (!result) return false;
                  }
                  return true;
                })
                .TypeConverter<ToMonthEndArrayConverter>().Optional();

            Map(m => m.ProtectLocation).Name("protect_location")
                .TypeConverterOption.NullValues(string.Empty).Optional();

        }
    }

}
