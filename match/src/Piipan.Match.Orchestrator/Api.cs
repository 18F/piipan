using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Piipan.Match.Shared;
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
            try
            {
                log.LogInformation("Executing request from user {User}", req.HttpContext?.User.Identity.Name);

                var incoming = await new StreamReader(req.Body).ReadToEndAsync();
                var request = Parse(incoming, log);
                // Top-level request validation
                var requestvalidateResult = (new OrchMatchRequestValidator()).Validate(request);
                if (!requestvalidateResult.IsValid)
                {
                    // Incoming request could not be deserialized
                    return ValidationErrorResponse(requestvalidateResult);
                }

                var orchResponse = new OrchMatchResponse();
                var personsValidator = new PersonValidator();
                for (int i = 0; i < request.Data.Count; i++)
                {
                    var result = new OrchMatchResult();
                    try
                    {
                        var person = request.Data[i];
                        // person-level validation
                        var personValidatResult = personsValidator.Validate(person);
                        if (!personValidatResult.IsValid)
                        {
                            // person-level validation error
                            foreach (var failure in personValidatResult.Errors)
                            {
                                log.LogError($"Property: {failure.PropertyName}, Error Code: {failure.ErrorCode}");
                                // this can result in multiple error objects for one person
                                orchResponse.Data.Errors.Add(new OrchMatchError()
                                {
                                    Index = i,
                                    Code = failure.ErrorCode,
                                    Detail = failure.ErrorMessage
                                });
                            }
                            continue;
                        }

                        PersonMatchRequest personRequest = new PersonMatchRequest();
                        personRequest.Query = new PersonMatchQuery {
                            Last = person.Last,
                            First = person.First,
                            Middle = person.Middle,
                            Dob = person.Dob,
                            Ssn = person.Ssn
                        };
                        result.Index = i;
                        result.Matches = await PersonMatch(personRequest, log);

                        if (result.Matches.Count > 0)
                        {
                            result.LookupId = await Lookup.Save(person, _lookupStorage, log);
                        }
                        orchResponse.Data.Results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        // Exception when attempting state-level matches
                        log.LogError(ex.Message);

                        orchResponse.Data.Errors.Add(new OrchMatchError() {
                            Index = i,
                            Code = ex.GetType().Name,
                            Detail = ex.Message
                        });
                    }

                }
                return (ActionResult)new JsonResult(orchResponse);
            }
            catch (Exception topLevelEx)
            {
                log.LogError(topLevelEx.Message);
                return InternalServerErrorResponse(topLevelEx);
            }
        }

        /// <summary>
        /// API endpoint for retrieving a MatchQuery using a lookup ID
        /// </summary>
        /// <param name="req">incoming HTTP request</param>
        /// <param name="lookupId">lookup ID string (pulled from route)</param>
        /// <param name="log">handle to the function log</param>
        [FunctionName("lookup_ids")]
        public async Task<IActionResult> LookupIds(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "lookup_ids/{lookupId}")] HttpRequest req,
            string lookupId,
            ILogger log)
        {
            log.LogInformation("Executing request from user {User}", req.HttpContext?.User.Identity.Name);

            LookupResponse response = new LookupResponse { Data = null };
            response.Data = await Lookup.Retrieve(lookupId, _lookupStorage, log);

            return (ActionResult)new JsonResult(response);
        }

        private OrchMatchRequest Parse(string requestBody, ILogger log)
        {
            // Assume failure
            OrchMatchRequest request = new OrchMatchRequest();

            try
            {
                request = JsonConvert.DeserializeObject<OrchMatchRequest>(requestBody);
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);
            }

            return request;
        }

        private bool Validate(OrchMatchRequest request, ILogger log)
        {
            OrchMatchRequestValidator validator = new OrchMatchRequestValidator();
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

        private async Task<MatchResponse> PerStateMatch(Uri uri, PersonMatchRequest request, ILogger log)
        {
            var content = new StringContent(JsonConvert.SerializeObject(request));
            var response = await _apiClient.PostAsync(uri, content);

            response.EnsureSuccessStatusCode();

            var matchResponse = await response.Content.ReadAsAsync<MatchResponse>();

            return matchResponse;
        }

        private async Task<List<PiiRecord>> PersonMatch(PersonMatchRequest request, ILogger log)
        {
            var matches = new List<PiiRecord>();
            var stateRequests = new List<Task<MatchResponse>>();
            var stateApiUris = StateApiUris();

            foreach (var uri in stateApiUris)
            {
                stateRequests.Add(PerStateMatch(uri, request, log));
            }

            await Task.WhenAll(stateRequests.ToArray());

            foreach (var stateRequest in stateRequests)
            {
                matches.AddRange(stateRequest.Result.Matches);
            }

            return matches;
        }

        private ActionResult ValidationErrorResponse(
            FluentValidation.Results.ValidationResult result
        )
        {
            var errResponse = new ApiErrorResponse();
            foreach (var failure in result.Errors)
            {
                errResponse.Errors.Add(new ApiHttpError()
                {
                    Status = Convert.ToString((int)HttpStatusCode.BadRequest),
                    Title = failure.ErrorCode,
                    Detail = failure.ErrorMessage
                });
            }
            return (ActionResult)new BadRequestObjectResult(errResponse);
        }

        private ActionResult InternalServerErrorResponse(Exception ex)
        {
            var errResponse = new ApiErrorResponse();
            errResponse.Errors.Add(new ApiHttpError()
            {
                Status = Convert.ToString((int)HttpStatusCode.InternalServerError),
                Title = ex.GetType().Name,
                Detail = ex.Message
            });
            return (ActionResult)new JsonResult(errResponse)
            {
                StatusCode = (int)HttpStatusCode.InternalServerError
            };
        }
    }
}
