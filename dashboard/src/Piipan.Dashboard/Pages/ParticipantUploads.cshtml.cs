using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Piipan.Dashboard.Api;

#nullable enable

namespace Piipan.Dashboard.Pages
{
    public class ParticipantUploadsModel : PageModel
    {
        private readonly IParticipantUploadRequest _participantUploadRequest;
        private readonly ILogger<ParticipantUploadsModel> _logger;

        public ParticipantUploadsModel(IParticipantUploadRequest participantUploadRequest, ILogger<ParticipantUploadsModel> logger)
        {
            _participantUploadRequest = participantUploadRequest;
            _logger = logger;
        }
        public string Title = "Participant Uploads";
        public List<ParticipantUpload> ParticipantUploadResults { get; private set; } = new List<ParticipantUpload>();
        public string? NextPageParams { get; private set; }
        public string? PrevPageParams { get; private set; }
        public string? StateQuery { get; private set; }
        public static int PerPageDefault = 10;
        public static string ApiUrlKey = "MetricsApiUri";
        public string? BaseUrl = Environment.GetEnvironmentVariable(ApiUrlKey);

        private HttpClient httpClient = new HttpClient();

        public async Task OnGetAsync()
        {
            try
            {
                _logger.LogInformation("Loading initial results");
                var url = FormatUrl();
                var response = await _participantUploadRequest.Get(url);
                ParticipantUploadResults = response.data;
                SetPageLinks(response.meta);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, exception.Message);
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                _logger.LogInformation("Querying uploads via search form");

                if (BaseUrl == null)
                {
                    throw new Exception("BaseUrl is null.");
                }

                StateQuery = Request.Form["state"];
                var url = QueryHelpers.AddQueryString(BaseUrl, "state", StateQuery);
                url = QueryHelpers.AddQueryString(url, "perPage", PerPageDefault.ToString());
                var response = await _participantUploadRequest.Get(url);
                ParticipantUploadResults = response.data;
                SetPageLinks(response.meta);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, exception.Message);
            }
            return Page();
        }

        // adds default pagination to the api url if none is present from request params
        private string FormatUrl()
        {
            if (BaseUrl == null)
            {
                throw new Exception("BaseUrl is null.");
            }
            var url = BaseUrl + Request.QueryString;
            StateQuery = Request.Query["state"];
            if (String.IsNullOrEmpty(Request.Query["perPage"]))
                url = QueryHelpers.AddQueryString(url, "perPage", PerPageDefault.ToString());
            return url;
        }

        private void SetPageLinks(ParticipantUploadResponseMeta meta)
        {
            NextPageParams = meta.nextPage;
            PrevPageParams = meta.prevPage;
        }
    }
}
