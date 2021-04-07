using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Piipan.Shared.Authentication;

namespace Piipan.QueryTool
{
    public class OrchestratorApiRequest
    {
        private readonly ILogger _logger;

        public OrchestratorApiRequest(IAuthorizedApiClient apiClient, ILogger logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        public string RequestUrl;
        public Dictionary<string, IQueryable> Query = new Dictionary<string, IQueryable>();
        private readonly IAuthorizedApiClient _apiClient;

        public async Task<List<PiiRecord>> SendQuery(string url, IQueryable query)
        {
            RequestUrl = url;
            Query.Add("query", query);
            return await QueryOrchestrator();
        }

        public List<PiiRecord> Matches { get; private set; }

        private async Task<List<PiiRecord>> QueryOrchestrator()
        {
            try
            {
                _logger.LogInformation("Querying Orchestrator API");
                var requestUri = new Uri(RequestUrl);
                var jsonString = JsonSerializer.Serialize(Query);
                var requestBody = new StringContent(jsonString, Encoding.UTF8, "application/json");
                HttpResponseMessage resp = null;

                if (Query["query"] is Lookup)
                {
                    var lookup = Query["query"] as Lookup;
                    requestUri = new Uri(requestUri, $"lookup_ids/{lookup.LookupId}");
                    resp = await _apiClient.GetAsync(requestUri);
                }
                else if (Query["query"] is PiiRecord)
                {
                    resp = await _apiClient.PostAsync(requestUri, requestBody);
                }

                var streamTask = await resp.Content.ReadAsStreamAsync();
                var json = await JsonSerializer.DeserializeAsync<OrchestratorApiResponse>(streamTask);
                Matches = json.matches;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, exception.Message);
            }
            return Matches;
        }
    }

    public class OrchestratorApiResponse
    {
        public virtual List<PiiRecord> matches { get; set; }
    }
}
