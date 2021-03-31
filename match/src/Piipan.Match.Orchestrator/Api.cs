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
using Piipan.Shared.Authentication;

namespace Piipan.Match.Orchestrator
{
    /// <summary>
    /// Azure Function implementing orchestrator matching API.
    /// </summary>
    public class Api
    {
        private readonly IAuthorizedApiClient _apiClient;
        private readonly ITableStorage<QueryEntity> _lookupStorage;

        public Api(IAuthorizedApiClient apiClient, ITableStorage<QueryEntity> lookupStorage)
        {
            _apiClient = apiClient;
            _lookupStorage = lookupStorage;
        }

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
        public async Task<IActionResult> Query(
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
                response.Matches = await Match(request, log);

                if (response.Matches.Count > 0)
                {
                    response.LookupId = await Lookup.Save(request.Query, _lookupStorage);
                }
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

        private MatchQueryRequest Parse(string requestBody, ILogger log)
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

        private bool Validate(MatchQueryRequest request, ILogger log)
        {
            MatchQueryRequestValidator validator = new MatchQueryRequestValidator();
            var result = validator.Validate(request);

            if (!result.IsValid)
            {
                log.LogError(result.ToString());
            }

            return result.IsValid;
        }

        private IEnumerable<Uri> StateApiUris()
        {
            const string StateApiUriStrings = "StateApiUriStrings";

            // XXX Validate input
            IEnumerable<Uri> uris = JsonConvert.DeserializeObject<IEnumerable<Uri>>(
                Environment.GetEnvironmentVariable(StateApiUriStrings));

            return uris;
        }

        private async Task<MatchQueryResponse> MatchState(Uri uri, MatchQueryRequest request, ILogger log)
        {
            var content = new StringContent(JsonConvert.SerializeObject(request));
            var response = await _apiClient.PostAsync(uri, content);

            response.EnsureSuccessStatusCode();

            var matchResponse = await response.Content.ReadAsAsync<MatchQueryResponse>();

            return matchResponse;
        }

        private async Task<List<PiiRecord>> Match(MatchQueryRequest request, ILogger log)
        {
            var matches = new List<PiiRecord>();
            var stateRequests = new List<Task<MatchQueryResponse>>();
            var stateApiUris = StateApiUris();

            // Loop through each state, compile results
            // XXX Refactor to leverage async operations
            foreach (var uri in stateApiUris)
            {
                var stateMatches = await MatchState(uri, request, log);
                matches.AddRange(stateMatches.Matches);
            }

            return matches;
        }
    }
}
