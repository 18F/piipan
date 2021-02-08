using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Npgsql;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Piipan.Metrics.Models;
using Piipan.Metrics.Api.Serializers;

namespace Piipan.Metrics.Api
{
    public static class GetParticipantUploads
    {
        [FunctionName("GetParticipantUploads")]
        public static async Task<OkObjectResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            try
            {
                var data = await Read(
                    NpgsqlFactory.Instance,
                    log
                );
                var response = new ParticipantUploadsResponse(data, data.Count);

                return new OkObjectResult(
                    JsonConvert.SerializeObject(response, Formatting.Indented)
                );
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);
                throw;
            }
        }

        public async static Task<List<ParticipantUpload>> Read(
            DbProviderFactory factory,
            ILogger log)
        {
            List<ParticipantUpload> results = new List<ParticipantUpload>();
            string connString = await ConnectionString(log);
            using (var conn = factory.CreateConnection())
            {
                conn.ConnectionString = connString;
                log.LogInformation("Opening db connection");
                conn.Open();
                using (var cmd = factory.CreateCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = "SELECT state, uploaded_at FROM participant_uploads ORDER BY uploaded_at DESC LIMIT 50";
                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var record = new ParticipantUpload
                        {
                            state = reader[0].ToString(),
                            uploaded_at = Convert.ToDateTime(reader[1])
                        };
                        results.Add(record);
                    }
                }
                conn.Close();
                log.LogInformation("Closed db connection");
            }
            return results;
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
    }
}
