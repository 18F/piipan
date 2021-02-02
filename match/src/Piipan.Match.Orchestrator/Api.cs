using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Piipan.Match.Orchestrator
{
    /// <summary>
    /// Azure Function implementing orchestrator matching API.
    /// </summary>
    public static class Api
    {
        static readonly HttpClient client = new HttpClient();

        /// <summary>
        /// API endpoint for conducting a PII match across all participating states
        /// </summary>
        /// <param name="req">incoming HTTP request</param>
        /// <param name="log">handle to the function log</param>
        /// <remarks>
        /// This function is expected to be executing as a resource with query
        /// access to the individual per-state API resources.
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

            var response = new MatchQueryResponse();
            try
            {
                response.Matches = await Match(request, client, log);
            }
            catch (Exception ex)
            {
                // Exception when attempting state-level matches, fail with 500
                // XXX fine-grained, per-state handling
                log.LogError(ex.Message);
                return (ActionResult)new InternalServerErrorResult();
            }

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

        internal static IEnumerable<string> ApiEndpoints()
        {
            const string StateApiEndpointStrings = "StateApiEndpointStrings";

            IEnumerable<string> endpoints = JsonConvert.DeserializeObject<IEnumerable<string>>(
                Environment.GetEnvironmentVariable(StateApiEndpointStrings));

            return endpoints;
        }

        internal async static Task<MatchQueryResponse> MatchState(string uri, MatchQueryRequest request, HttpClient client, ILogger log)
        {
            var response = await client.PostAsJsonAsync(uri, request);
            var matchResponse = await response.Content.ReadAsAsync<MatchQueryResponse>();

            // Exception caught by `Query`
            response.EnsureSuccessStatusCode();

            return matchResponse;
        }

        internal async static Task<List<PiiRecord>> Match(MatchQueryRequest request, HttpClient client, ILogger log)
        {
            List<PiiRecord> matches = new List<PiiRecord>();
            var endpoints = ApiEndpoints();

            // Loop through each state, compile results
            foreach (var uri in endpoints)
            {
                var stateMatches = await MatchState(uri, request, client, log);
                matches.AddRange(stateMatches.Matches);
            }

            return matches;
        }
    }
}