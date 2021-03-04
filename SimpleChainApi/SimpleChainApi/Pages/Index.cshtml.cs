using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SimpleChainApi.Services;

namespace RestAPIClient.Pages
{
    public class IndexModel : PageModel
    {
        private const string DEEPTH = "DEEPTH";
        private readonly ILogger<IndexModel> logger;
        private readonly IDependencyCallerService dependencyCallerService;

        public IndexModel(IConfiguration configuration, ILogger<IndexModel> logger, IDependencyCallerService dependencyCallerService)
        {
            Deep = configuration[DEEPTH];
            this.logger = logger;
            this.dependencyCallerService = dependencyCallerService;
        }

        [BindProperty]
        public string Deep { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                int deep = 0;
                int.TryParse(Deep, out deep);
                var response = await dependencyCallerService.ComputeDependenciesAsync(deep);
                
                return RedirectToPage("Response", new { result = JsonConvert.SerializeObject(response) });
            }
            catch (ArgumentNullException uex)
            {
                logger.LogError("ArgumentNullException {uex}", uex);
                return RedirectToPage("Error", new { msg = uex.Message + " | URL missing or invalid." });
            }
            catch (JsonReaderException jex)
            {
                logger.LogError("JsonReaderException {jex}", jex);
                return RedirectToPage("Error", new { msg = jex.Message + " | Json data could not be read." });
            }
            catch (Exception ex)
            {
                logger.LogError("Exception {uex}", ex);
                return RedirectToPage("Error", new { msg = ex.Message + " | Are you missing some Json keys and values? Please check your Json data." });
            }
        }
    }
}
