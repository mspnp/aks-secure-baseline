using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SimpleChainApi.Services;
using System;
using System.Threading.Tasks;

namespace SimpleChainApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class URLCallerController : ControllerBase
    {

        private readonly ILogger<URLCallerController> _logger;

        private readonly IDependencyCallerService _dependencyCallerService;

        public URLCallerController(ILogger<URLCallerController> logger, IDependencyCallerService dependencyCallerService)
        {
            _logger = logger;
            _dependencyCallerService = dependencyCallerService;
        }

        [HttpGet]
        [Route("depth/{depth:int}")]
        public async Task<DependencyResult> GetAsync(int depth)
        {
            try
            {
                return await _dependencyCallerService.ComputeDependenciesAsync(depth);
            }
            catch (Exception ex)
            {
                _logger.LogError("Exception {ex}", ex);
                throw;
            }
        }
    }
}
