using System;
using System.Net;
using System.Text.Json.Serialization;

namespace SimpleChainApi
{
    public class UrlCalled
    {
        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        [JsonPropertyName("uri")]
        public string Uri { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("statusCode")]
        public HttpStatusCode StatusCode { get; set; }
    }
}
