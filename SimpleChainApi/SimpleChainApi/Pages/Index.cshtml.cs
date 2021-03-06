using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SimpleChainApi;
using SimpleChainApi.Services;

namespace RestAPIClient.Pages
{
    public class IndexModel : PageModel
    {
        private const string DEPTH = "DEPTH";

        private readonly ILogger<IndexModel> logger;

        private readonly IDependencyCallerService dependencyCallerService;

        public string FormatedResult { get; set; }
        public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration, IDependencyCallerService dependencyCallerService)
        {
            Deep = string.IsNullOrWhiteSpace(configuration[DEPTH]) ? "0" : configuration[DEPTH];
            this.logger = logger;
            this.dependencyCallerService = dependencyCallerService;
        }

        [BindProperty]
        public string Deep { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                int deep = 0;
                _ = int.TryParse(Deep, out deep);
                var response = await dependencyCallerService.ComputeDependenciesAsync(deep);
                StringBuilder stringBuilder = new StringBuilder();
                Format(response, stringBuilder);
                FormatedResult = stringBuilder.ToString();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in OnGetAsync");
            }
        }

        public void Format(DependencyResult dependencyResult, StringBuilder sb)
        {
            if (dependencyResult != null && (dependencyResult.ExternalDependencies.Any() || dependencyResult.SelfCalled.Any()))
            {
                var externalCalls = dependencyResult.ExternalDependencies.Count();
                var internalCalls = dependencyResult.SelfCalled.Count();

                sb.AppendLine($"<p>This service directly attempted the following <strong>{"calls".ToQuantity(externalCalls + internalCalls, ShowQuantityAs.Words)}</strong>.</p>");
                sb.AppendLine($"<div style='border-left: lightgray 2px dashed; padding-left: 5px; margin-left: 8px;'>");
                sb.AppendLine($"<label>{"External Call".ToQuantity(externalCalls)}:</label><ul>");
                foreach (var externalDependency in dependencyResult.ExternalDependencies)
                {
                    var color = externalDependency.Success ? "text-success" : "text-danger";
                    sb.Append($"<li><span class=\"{color}\">");
                    if (externalDependency.Success)
                    {
                        sb.Append($"Successfully connected to <i><strong>{externalDependency.Uri}</strong></i>.");
                    }
                    else
                    {
                        sb.Append($"Connection to <i><strong>{externalDependency.Uri}</strong></i> could not be established.");
                    }
                    sb.Append($" [~{TimeSpan.FromMilliseconds(externalDependency.RequestTimeIsMs).Humanize()}]</span></li>");
                }
                sb.Append("</ul>");

                sb.AppendLine($"<label>{"Internal Call".ToQuantity(internalCalls)}:</label><ul>");
                foreach (var selfCalled in dependencyResult.SelfCalled)
                {
                    var color = selfCalled.Success ? "text-success" : "text-danger";
                    sb.Append($"<li><span class=\"{color}\">");
                    if (selfCalled.Success)
                    {
                        sb.Append($"Successfully connected to <i><strong>{selfCalled.Uri}</strong></i>. [~{TimeSpan.FromMilliseconds(selfCalled.RequestTimeIsMs).Humanize()}]</span>");

                        if (selfCalled == null || selfCalled.DependencyResult == null  || (!selfCalled.DependencyResult.ExternalDependencies.Any() && !selfCalled.DependencyResult.SelfCalled.Any()))
                        { 
                            sb.Append("<p><i>This service did not attempt any additional connections.</i></p>");
                        }

                        Format(selfCalled.DependencyResult, sb);
                    }
                    else
                    {
                        sb.Append($"Connection to <i><strong>{selfCalled.Uri}</strong></i> could not be established. [~{TimeSpan.FromMilliseconds(selfCalled.RequestTimeIsMs).Humanize()}]</span>");
                    }
                    sb.Append("</li>");
                }
                sb.Append("</ul>");

                sb.AppendLine("</div>");
            }
        }
    }
}
