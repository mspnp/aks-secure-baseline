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
        private const string DEPTH = "DEPTH";


        public IndexModel(IConfiguration configuration)
        {
            Deep = string.IsNullOrWhiteSpace(configuration[DEPTH]) ? "0" : configuration[DEPTH];
        }

        [BindProperty]
        public string Deep { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            return RedirectToPage("Response", new { depth = Deep });
        }
    }
}
