using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimpleChainApi.Services;
using System;

namespace SimpleChainApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddControllers();
            services.AddHttpClient(Options.DefaultName, c =>
            {
                c.Timeout = TimeSpan.FromSeconds(14);
            });
            services.AddRazorPages();
            services.AddTransient<IDependencyCallerService, DependencyCallerService>();
        }

        public void Configure(IApplicationBuilder app, ILogger<Startup> logger)
        {
            logger.LogInformation("Starting.");

            app.UseRouting();
            app.UseStaticFiles();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllers();
            });
        }
    }
}
