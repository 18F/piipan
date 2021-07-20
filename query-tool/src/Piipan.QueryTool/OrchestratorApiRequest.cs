using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Piipan.Shared.Authentication;

namespace Piipan.QueryTool
{
    public class OrchestratorApiRequest
    {
        private readonly ILogger _logger;
        private readonly IAuthorizedApiClient _apiClient;
        private readonly Uri _baseUri;

        public OrchestratorApiRequest(IAuthorizedApiClient apiClient, Uri baseUri, ILogger logger)
        {
            _apiClient = apiClient;
            _baseUri = baseUri;
            _logger = logger;
        }

        public async Task<MatchResponse> Match(PiiRecord query)
        {
            const string Endpoint = "query";

            var request = new MatchRequest { Data = new List<PiiRecord> { query } };
            var json = JsonSerializer.Serialize(request);
            var response = await _apiClient.PostAsync(
                new Uri(_baseUri, Endpoint),
                new StringContent(json)
            );

            response.EnsureSuccessStatusCode();

            var matchJson = await response.Content.ReadAsStringAsync();
            var matchResponse = JsonSerializer.Deserialize<MatchResponse>(matchJson);

            return matchResponse;
        }

        public async Task<LookupResponse> Lookup(string lookupId)
        {
            const string Endpoint = "lookup_ids";

            var uri = new Uri(_baseUri, $"{Endpoint}/{lookupId}");
            var response = await _apiClient.GetAsync(uri);

            response.EnsureSuccessStatusCode();

            var lookupJson = await response.Content.ReadAsStringAsync();
            var lookupResponse = JsonSerializer.Deserialize<LookupResponse>(lookupJson);

            return lookupResponse;
        }
    }

    public class MatchResponse
    {
        [JsonPropertyName("data")]
        public MatchResponseData Data { get; set; } = new MatchResponseData();
    }

    public class MatchResponseData
    {
        [JsonPropertyName("results")]
        public List<MatchResult> Results { get; set; } = new List<MatchResult>();

        [JsonPropertyName("errors")]
        public List<MatchError> Errors { get; set; } = new List<MatchError>();
    }

    public class MatchResult
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("lookup_id")]
        public virtual string LookupId { get; set; }

        [JsonPropertyName("matches")]
        public virtual List<PiiRecord> Matches { get; set; }
    }

    public class MatchError
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("detail")]
        public string Detail { get; set; }
    }

    public class LookupResponse
    {
        [JsonPropertyName("data")]
        public virtual PiiRecord data { get; set; }
    }
}
