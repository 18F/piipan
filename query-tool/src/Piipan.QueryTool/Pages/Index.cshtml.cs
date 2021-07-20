using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Piipan.Shared.Authentication;

namespace Piipan.QueryTool.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IAuthorizedApiClient _apiClient;
        private readonly OrchestratorApiRequest _apiRequest;

        public IndexModel(ILogger<IndexModel> logger,
                          IAuthorizedApiClient apiClient)
        {
            _logger = logger;
            _apiClient = apiClient;
            var apiBaseUri = new Uri(Environment.GetEnvironmentVariable("OrchApiUri"));
            _apiRequest = new OrchestratorApiRequest(_apiClient, apiBaseUri, _logger);
        }

        [BindProperty]
        public PiiRecord Query { get; set; }
        public MatchResponse QueryResult { get; private set; }
        public String RequestError { get; private set; }
        public bool NoResults = false;

        public async Task<IActionResult> OnPostAsync()
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _logger.LogInformation("Query form submitted");

                    MatchResponse result = await _apiRequest.Match(Query);

                    QueryResult = result;
                    NoResults = QueryResult.Data.Results.Count == 0 ||
                        QueryResult.Data.Results[0].Matches.Count == 0;
                    Title = "NAC Query Results";
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, exception.Message);
                    RequestError = "There was an error running your search";
                }
            }

            return Page();
        }

        public string Title { get; private set; } = "";

        public void OnGet()
        {
            Title = "NAC Query Tool";
        }
    }
}
