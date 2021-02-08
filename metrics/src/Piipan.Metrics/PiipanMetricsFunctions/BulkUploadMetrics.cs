// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Npgsql;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace PiipanMetricsFunctions
{
    public static class BulkUploadMetrics
    {
        /// <summary>
        /// Listens for BulkUpload events when users upload participants;
        /// write meta info to Metrics database
        /// </summary>
        /// <param name="eventGridEvent">storage container blob creation event</param>
        /// <param name="log">handle to the function log</param>

        [FunctionName("BulkUploadMetrics")]
        public async static Task Run(
            [EventGridTrigger]EventGridEvent eventGridEvent,
            ILogger log)
        {
            log.LogInformation(eventGridEvent.Data.ToString());
            try
            {
                string state = ParseState(eventGridEvent);
                await Write(
                    state,
                    eventGridEvent.EventTime,
                    NpgsqlFactory.Instance,
                    log
                );
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);
                throw;
            }
        }

        private static string ParseState(
            [EventGridTrigger] EventGridEvent eventGridEvent
        )
        {
            var jsondata = JsonConvert.SerializeObject(eventGridEvent.Data);
            var tmp = new { url = "" };
            var data = JsonConvert.DeserializeAnonymousType(jsondata, tmp);

            Regex regex = new Regex("^https://([a-z]+)state");
            Match match = regex.Match(data.url);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            else
            {
                throw new FormatException("State not found");
            }
        }

        internal async static Task<string> ConnectionString(ILogger log)
        {
            // Environment variable (and placeholder) established
            // during initial function app provisioning in IaC
            const string DatabaseConnectionString = "DatabaseConnectionString";
            const string PasswordPlaceholder = "{password}";
            const string secretName = "metrics-pg-admin";
            const string vaultName = "metrics-secret-keeper";
            var kvUri = $"https://{vaultName}.vault.azure.net";

            var builder = new NpgsqlConnectionStringBuilder(
                Environment.GetEnvironmentVariable(DatabaseConnectionString));

            var client = new SecretClient(new Uri(kvUri), new DefaultAzureCredential());

            if (builder.Password == PasswordPlaceholder)
            {
                var secret = await client.GetSecretAsync(secretName);
                builder.Password = $"{secret.Value.Value}";
            }

            return builder.ConnectionString;
        }

        public async static Task Write(
            String state,
            DateTime uploadedAt,
            DbProviderFactory factory,
            ILogger log)
        {
            string connString = await ConnectionString(log);
            using (var conn = factory.CreateConnection())
            {
                conn.ConnectionString = connString;
                log.LogInformation("Opening db connection");
                conn.Open();
                var tx = conn.BeginTransaction();

                // Is there a way to get this db table name dynamically? Otherwise We'd have to remember to change the name in both the iac script and here.
                using (var cmd = factory.CreateCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = "INSERT INTO participant_uploads (state, uploaded_at) VALUES (@state, @uploaded_at)";
                    AddWithValue(cmd, DbType.String, "state", state);
                    AddWithValue(cmd, DbType.DateTime, "uploaded_at", uploadedAt);
                    int nRows = cmd.ExecuteNonQuery();
                    log.LogInformation(String.Format("Number of rows inserted={0}", nRows));
                }
                tx.Commit();
                conn.Close();
                log.LogInformation("db connection closed");
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
    }
}
