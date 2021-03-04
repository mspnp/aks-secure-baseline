using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace SimpleChainApi
{
    public class DependencyResult
    {
        public DependencyResult()
        {
            ExternalDependencies = Enumerable.Empty<URLCalled>();
            SelfCalled = Enumerable.Empty<SelfDependencyCalled>();
        }

        [JsonPropertyName("externalDependencies")]
        public IEnumerable<URLCalled> ExternalDependencies { get; set; }

        [JsonPropertyName("selfCalled")]
        public IEnumerable<SelfDependencyCalled> SelfCalled { get; set; }
    }
}
