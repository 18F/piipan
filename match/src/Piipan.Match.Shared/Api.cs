using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;

namespace Piipan.Match.Shared
{
    /// <summary>
    /// Represents a generic error response for an API request
    /// </summary>
    public class ApiErrorResponse
    {
        [JsonProperty("errors", Required = Required.Always)]
        public List<ApiHttpError> Errors { get; set; } = new List<ApiHttpError>();
    }

    /// <summary>
    /// Represents http-level and other top-level errors for an API request
    /// <para> Use for items in the Errors list in the top-level API response</para>
    /// </summary>
    public class ApiHttpError
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("detail")]
        public string Detail { get; set; }
    }
}
