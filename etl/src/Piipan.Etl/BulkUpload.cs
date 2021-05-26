// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Piipan.Etl
{
    /// <summary>
    /// Azure Function implementing basic Extract-Transform-Load of piipan
    /// bulk import CSV files via Storage Containers, Event Grid, and
    /// PostgreSQL.
    /// </summary>
    public static class BulkUpload
    {
        /// <summary>
        /// Entry point for the state-specific Azure Function instance
        /// </summary>
        /// <param name="eventGridEvent">storage container blob creation event</param>
        /// <param name="input">handle to CSV file uploaded to a state-specific container</param>
        /// <param name="log">handle to the function log</param>
        /// <remarks>
        /// The function is expected to be executing as a managed identity that has read access
        /// to the per-state storage account and write access to the per-state database.
        /// </remarks>
        [FunctionName("BulkUpload")]
        public async static Task Run(
            [EventGridTrigger] EventGridEvent eventGridEvent,
            [Blob("{data.url}", FileAccess.Read, Connection = "BlobStorageConnectionString")] Stream input,
            ILogger log)
        {
            log.LogInformation(eventGridEvent.Data.ToString());

            try
            {
                if (input != null)
                {
                    var records = Read(input, log);
                    await Load(records, NpgsqlFactory.Instance, log);
                }
                else
                {
                    // Can get here if Function does not have
                    // permission to access blob URL
                    log.LogError("No input stream was provided");
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);
                throw;
            }
        }

        public static DateTime LastDayOfMonth(DateTime dateTime)
        {
            return new DateTime(dateTime.Year, dateTime.Month, DateTime.DaysInMonth(dateTime.Year, dateTime.Month));
        }

        internal static IEnumerable<PiiRecord> Read(Stream input, ILogger log)
        {
            var reader = new StreamReader(input);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim
            };
            var csv = new CsvReader(reader, config);
            csv.Context.RegisterClassMap<PiiRecordMap>();

            // Yields records as it is iterated over
            return csv.GetRecords<PiiRecord>();
        }

        internal async static Task<string> ConnectionString()
        {
            // Environment variables (and placeholder) established
            // during initial function app provisioning in IaC
            const string CloudName = "CloudName";
            const string DatabaseConnectionString = "DatabaseConnectionString";
            const string PasswordPlaceholder = "{password}";
            const string GovernmentCloud = "AzureUSGovernment";

            // Resource ids for open source software databases in the public and
            // US government clouds. Set the desired active cloud, then see:
            // `az cloud show --query endpoints.ossrdbmsResourceId`
            const string CommercialId = "https://ossrdbms-aad.database.windows.net";
            const string GovermentId = "https://ossrdbms-aad.database.usgovcloudapi.net";

            var resourceId = CommercialId;
            var cn = Environment.GetEnvironmentVariable(CloudName);
            if (cn == GovernmentCloud) {
                resourceId = GovermentId;
            }

            var builder = new NpgsqlConnectionStringBuilder(
                Environment.GetEnvironmentVariable(DatabaseConnectionString));

            if (builder.Password == PasswordPlaceholder)
            {
                var provider = new AzureServiceTokenProvider();
                var token = await provider.GetAccessTokenAsync(resourceId);
                builder.Password = token;
            }

            return builder.ConnectionString;
        }

        internal async static Task Load(IEnumerable<PiiRecord> records, DbProviderFactory factory, ILogger log)
        {
            var connString = await ConnectionString();

            using (var conn = factory.CreateConnection())
            {
                conn.ConnectionString = connString;
                conn.Open();

                // Assumes we want to process in an all-or-nothing fashion;
                // i.e., a processing error in one record spoils the whole batch
                var tx = conn.BeginTransaction();

                using (var cmd = factory.CreateCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = "INSERT INTO uploads (created_at, publisher) VALUES(now(), current_user)";
                    cmd.ExecuteNonQuery();
                }

                Int64 lastval = 0;
                using (var cmd = factory.CreateCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = "SELECT lastval()";
                    lastval = (Int64)cmd.ExecuteScalar();
                }

                foreach (var record in records)
                {
                    if (record.BenefitsEndDate.HasValue)
                    {
                      DateTime benefitsEndDate = record.BenefitsEndDate.Value;
                      record.BenefitsEndDate = LastDayOfMonth(benefitsEndDate);
                    }

                    using (var cmd = factory.CreateCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "INSERT INTO participants (last, first, middle, dob, ssn, exception, upload_id, case_id, participant_id, benefits_end_date, recent_benefit_months) " +
                            "VALUES (@last, @first, @middle, @dob, @ssn, @exception, @upload_id, @case_id, @participant_id, @benefits_end_date, @recent_benefit_months::date[])";

                        AddWithValue(cmd, DbType.String, "last", record.Last);
                        AddWithValue(cmd, DbType.String, "first", (object)record.First ?? DBNull.Value);
                        AddWithValue(cmd, DbType.String, "middle", (object)record.Middle ?? DBNull.Value);
                        AddWithValue(cmd, DbType.DateTime, "dob", record.Dob);
                        AddWithValue(cmd, DbType.String, "ssn", record.Ssn);
                        AddWithValue(cmd, DbType.String, "exception", (object)record.Exception ?? DBNull.Value);
                        AddWithValue(cmd, DbType.Int64, "upload_id", lastval);
                        AddWithValue(cmd, DbType.String, "case_id", record.CaseId);
                        AddWithValue(cmd, DbType.String, "participant_id", (object)record.ParticipantId ?? DBNull.Value);
                        AddWithValue(cmd, DbType.DateTime, "benefits_end_date", (object)record.BenefitsEndDate ?? DBNull.Value);
                        AddWithValue(cmd, DbType.Object, "recent_benefit_months", (object)FormatDatesAsPgArray(record.RecentBenefitMonths) ?? DBNull.Value);

                        cmd.ExecuteNonQuery();
                    }
                }
                tx.Commit();
                conn.Close();
            }
        }

        static void AddWithValue(DbCommand cmd, DbType type, String name, object value)
        {
            var p = cmd.CreateParameter();
            p.DbType = type;
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }

        public static string FormatDatesAsPgArray(string input) {
            if (String.IsNullOrEmpty(input)) return "{}";
            string formatted = "";
            string[] datestrings = input.Split(' ');
            List<string> formattedDateStrings = new List<string>();
            formatted += "{";
            foreach (var datestring in datestrings)
            {
              if (String.IsNullOrEmpty(datestring)) continue;
              DateTime date = Convert.ToDateTime(datestring);
              formattedDateStrings.Add(date.ToString("yyyy-MM-dd"));
            }
            formatted += string.Join(",", formattedDateStrings);
            formatted += "}";
            return formatted;
        }
    }
}
