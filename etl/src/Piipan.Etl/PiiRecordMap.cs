using System.Text.RegularExpressions;
using CsvHelper.Configuration;

namespace Piipan.Etl
{
    /// <summary>
    /// Maps and validates a record from a CSV file formatted in accordance with
    /// <c>/doc/csv/import-schema.json</c> to a <c>PiiRecord</c>.
    /// </summary>
    public class PiiRecordMap : ClassMap<PiiRecord>
    {
        public PiiRecordMap()
        {
            Map(m => m.Last).Name("last").Validate(field =>
            {
                return !string.IsNullOrEmpty(field);
            });

            Map(m => m.First).Name("first")
                .TypeConverterOption.NullValues(string.Empty);

            Map(m => m.Middle).Name("middle")
                .TypeConverterOption.NullValues(string.Empty);

            Map(m => m.Dob).Name("dob");

            Map(m => m.Ssn).Name("ssn").Validate(field =>
            {
                Match match = Regex.Match(field, "^[0-9]{3}-[0-9]{2}-[0-9]{4}$");
                return match.Success;
            });

            Map(m => m.Exception).Name("exception")
                .TypeConverterOption.NullValues(string.Empty);
        }
    }
}