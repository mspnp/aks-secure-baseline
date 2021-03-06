using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
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
                var client = _clientFactory.CreateClient();
                var getExternalTask = ComputeExternalDependenciesAsync(client);
                var getInternalTask = ComputeSelfDependenciesAsync(client, depth);

                await Task.WhenAll(getExternalTask, getInternalTask);

                dependencyResult.ExternalDependencies = getExternalTask.Result;
                dependencyResult.SelfCalled = getInternalTask.Result;
            }
            else
            {
                _logger.LogInformation("Depth was 0, skipping dependency calls.");
            }

            return dependencyResult;
        }

        private async Task<List<UrlCalled>> ComputeExternalDependenciesAsync(HttpClient client)
        {
            var result = new List<UrlCalled>();
            var urlList = _configuration[EXTERNAL_DEPENDENCIES];
            _logger.LogInformation("URL external dependencies {urlList}", urlList);
            if (!string.IsNullOrWhiteSpace(urlList))
            {
                var urls = urlList.Split(',');
                if (urls.Any())
                {
                    var calls = urls.Select(url => CallExternalUrlAsync(client, url));
                    result.AddRange(await Task.WhenAll(calls));
                }
            }
            return result;
        }

        private static async Task<UrlCalled> CallExternalUrlAsync(HttpClient client, string url)
        {
            var urlCalledResult = new UrlCalled { Date = DateTime.UtcNow, Uri = url };
            var sw = Stopwatch.StartNew();
            try
            {
                var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                urlCalledResult.Success = response.IsSuccessStatusCode;
                urlCalledResult.StatusCode = response.StatusCode;
            }
            catch (HttpRequestException)
            {
                urlCalledResult.Success = false;
            }
            catch(TaskCanceledException)
            {
                // Timeout
                urlCalledResult.Success = false;
            } 
            finally
            {
                urlCalledResult.RequestTimeIsMs = sw.ElapsedMilliseconds;
            }

            return urlCalledResult;
        }

        private static async Task<SelfDependencyCalled> CallPeerServiceAsync(HttpClient client, string url)
        {
            var urlCalledResult = new SelfDependencyCalled { Date = DateTime.UtcNow, Uri = url };

            var sw = Stopwatch.StartNew();
            try
            {
                var response = await client.GetAsync(url);
                urlCalledResult.Success = response.IsSuccessStatusCode;
                urlCalledResult.StatusCode = response.StatusCode;
                urlCalledResult.RequestTimeIsMs = sw.ElapsedMilliseconds;

                if (urlCalledResult.Success)
                {
                    urlCalledResult.DependencyResult = await response.Content.ReadFromJsonAsync<DependencyResult>();
                }
            }
            catch (HttpRequestException)
            {
                urlCalledResult.Success = false;
            }
            catch (TaskCanceledException)
            {
                // Timeout
                urlCalledResult.Success = false;
            }
            finally
            {
                if (0 == urlCalledResult.RequestTimeIsMs)
                {
                    urlCalledResult.RequestTimeIsMs = sw.ElapsedMilliseconds;
                }
            }

            return urlCalledResult;
        }

        private async Task<List<SelfDependencyCalled>> ComputeSelfDependenciesAsync(HttpClient client, int depth)
        {
            var peerHostsConfigValue = _configuration[SELF_HOSTS_DEPENDENCIES];
            _logger.LogInformation("URL self dependencies {hostPortList}", peerHostsConfigValue);
            var result = new List<SelfDependencyCalled>();

            if (!string.IsNullOrWhiteSpace(peerHostsConfigValue) && depth > 0)
            {
                var newDepth = depth - 1;

                var peerHosts = peerHostsConfigValue.Split(',');
                if (peerHosts.Any())
                {
                    var calls = peerHosts.Select(peerHost => CallPeerServiceAsync(client, $"{peerHost}/URLCaller/depth/{newDepth}"));
                    result.AddRange(await Task.WhenAll(calls));
                }
            }

            return result;
        }
    }
}
