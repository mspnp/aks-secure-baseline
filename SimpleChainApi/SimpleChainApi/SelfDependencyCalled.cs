using System.Text.Json.Serialization;

namespace SimpleChainApi
{
    public class SelfDependencyCalled : UrlCalled
    {
        [JsonPropertyName("dependencyResult")]
        public DependencyResult DependencyResult { set; get; }
    }
}
