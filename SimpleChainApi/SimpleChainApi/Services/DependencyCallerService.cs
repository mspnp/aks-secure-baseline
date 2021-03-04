using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SimpleChainApi.Services
{
    public class DependencyCallerService : IDependencyCallerService
    {
        private const string EXTERNAL_DEPENDENCIES = "EXTERNAL_DEPENDENCIES";

        private const string SELF_HOSTS_DEPENDENCIES = "SELF_HOSTS_DEPENDENCIES";

        private readonly ILogger<DependencyCallerService> _logger;

        private readonly IHttpClientFactory _clientFactory;

        private readonly IConfiguration _configuration;

        public DependencyCallerService(ILogger<DependencyCallerService> logger, IConfiguration configuration, IHttpClientFactory clientFactory)
        {
            _logger = logger;
            _clientFactory = clientFactory;
            _configuration = configuration;
        }
        public async Task<DependencyResult> ComputeDependenciesAsync(int depth)
        {
            var dependencyResult = new DependencyResult();
            if (depth > 0)
            {
               
                await ComputeExternalDependenciesAsync(dependencyResult);

                await ComputeSelfDependenciesAsync(dependencyResult, depth);
            }

            return dependencyResult;
        }

        private async Task ComputeExternalDependenciesAsync(DependencyResult dependencyResult)
        {
            var client = _clientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var urlList = _configuration[EXTERNAL_DEPENDENCIES];
            _logger.LogInformation("URL external dependencies {urlList}", urlList);
            if (!string.IsNullOrWhiteSpace(urlList))
            {
                var result = new List<URLCalled>();
                foreach (var url in urlList.Split(','))
                {
                    var urlCalledResult = new URLCalled { Date = DateTime.Now, URI = url };
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    try
                    {
                        var response = await client.SendAsync(request);
                        urlCalledResult.Success = response.IsSuccessStatusCode;
                        urlCalledResult.StatusCode = response.StatusCode;
                    }
                    catch (HttpRequestException)
                    {
                        urlCalledResult.Success = false;
                    }
                    result.Add(urlCalledResult);
                }

                dependencyResult.ExternalDependencies = result;
            }
        }

        private async Task ComputeSelfDependenciesAsync(DependencyResult dependencyResult, int depth)
        {
            var client = _clientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(5);
            var hostPortList = _configuration[SELF_HOSTS_DEPENDENCIES];
            var newDepth = depth - 1;
            _logger.LogInformation("URL self dependencies {hostPortList}", hostPortList);
            if (!string.IsNullOrWhiteSpace(hostPortList) && depth > 0)
            {
                var result = new List<SelfDependencyCalled>();
                foreach (var hostPort in hostPortList.Split(','))
                {
                    var url = $"{hostPort}/URLCaller/depth/{newDepth}";
                    var urlCalledResult = new SelfDependencyCalled { Date = DateTime.Now, URI = url };
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    try
                    {
                        var response = await client.SendAsync(request);
                        urlCalledResult.Success = response.IsSuccessStatusCode;
                        urlCalledResult.StatusCode = response.StatusCode;
                        if (urlCalledResult.Success)
                        {
                            var innerDependencyResult = JsonSerializer.Deserialize<DependencyResult>(await response.Content.ReadAsStringAsync());
                            urlCalledResult.DependencyResult = innerDependencyResult;
                        }
                    }
                    catch (HttpRequestException)
                    {
                        urlCalledResult.Success = false;
                    }
                    result.Add(urlCalledResult);
                }

                dependencyResult.SelfCalled = result;
            }
        }
    }
}
