using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Npgsql;

namespace Piipan.Match.State
{
    /// <summary>
    /// Azure Function implementing per-state matching API for
    /// internal use by other subsystems and services.
    /// </summary>
    public static class Api
    {
        /// <summary>
        /// API endpoint for conducting a state-level match
        /// </summary>
        /// <param name="req">incoming HTTP request</param>
        /// <param name="log">handle to the function log</param>
        /// <remarks>
        /// This function is expected to be called internally with access
        /// restricted via networking, not authentication.
        /// </remarks>
        [FunctionName("query")]
        public static async Task<IActionResult> Query(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var incoming = await new StreamReader(req.Body).ReadToEndAsync();
            var request = Parse(incoming, log);
            if (request.Query == null)
            {
                // Incoming request could not be deserialized into MatchQueryResponse
                // XXX return validation messages
                return (ActionResult)new BadRequestResult();
            }

            if (!Validate(request, log))
            {
                // Request successfully deserialized but contains invalid properties
                // XXX return validation messages
                return (ActionResult)new BadRequestResult();
            }

            var response = new MatchQueryResponse
            {
                Matches = await Select(request, NpgsqlFactory.Instance, log)
            };
            return (ActionResult)new JsonResult(response);
        }

        internal static MatchQueryRequest Parse(string requestBody, ILogger log)
        {
            // Assume failure
            MatchQueryRequest request = new MatchQueryRequest { Query = null };

            try
            {
                request = JsonConvert.DeserializeObject<MatchQueryRequest>(requestBody);
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);
            }

            return request;
        }

        internal static bool Validate(MatchQueryRequest request, ILogger log)
        {
            MatchQueryRequestValidator validator = new MatchQueryRequestValidator();
            var result = validator.Validate(request);

            if (!result.IsValid)
            {
                log.LogError(result.ToString());
            }

            return result.IsValid;
        }

        internal static (string, dynamic) Prepare(MatchQueryRequest request, ILogger log)
        {
            var p = new
            {
                ssn = request.Query.Ssn,
                dob = request.Query.Dob,
                last = request.Query.Last,
                first = request.Query.First,
            };
            var sql = "SELECT upload_id, first, last, middle, dob, ssn, exception, case_id, participant_id FROM participants " +
                        "WHERE ssn=@ssn AND dob=@dob AND upper(last)=upper(@last) " +
                        "AND upload_id=(SELECT id FROM uploads ORDER BY id DESC LIMIT 1)";

            return (sql, p);
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
            if (cn == GovernmentCloud)
            {
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

        internal async static Task<List<PiiRecord>> Select(MatchQueryRequest request, DbProviderFactory factory, ILogger log)
        {
            List<PiiRecord> records;

            using (var conn = factory.CreateConnection())
            {
                conn.ConnectionString = await ConnectionString();
                conn.Open();

                (var sql, var parameters) = Prepare(request, log);
                records = conn.Query<PiiRecord>(sql, (object)parameters).AsList();

                conn.Close();
            }

            return records;
        }
    }
}
