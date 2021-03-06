using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace SimpleChainApi
{
    public class DependencyResult
    {
        public DependencyResult()
        {
            ExternalDependencies = Enumerable.Empty<UrlCalled>();
            SelfCalled = Enumerable.Empty<SelfDependencyCalled>();
        }

        [JsonPropertyName("externalDependencies")]
        public IEnumerable<UrlCalled> ExternalDependencies { get; set; }

        [JsonPropertyName("selfCalled")]
        public IEnumerable<SelfDependencyCalled> SelfCalled { get; set; }
    }
}
