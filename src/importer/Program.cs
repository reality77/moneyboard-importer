using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace importer
{
    class Program
    {
        static readonly IConfiguration _configuration;
        
        static Program()
        {
            var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            var confbuilder = new ConfigurationBuilder()
                .AddJsonFile($"appsettings.json", true, true)
                .AddJsonFile($"appsettings.{environmentName}.json", true, true)
                .AddEnvironmentVariables();

            _configuration = confbuilder.Build();
        }

        static async Task<int> Main(string[] args)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            
            using (ServiceProvider serviceProvider = services.BuildServiceProvider())
            {
                ImporterApp app = serviceProvider.GetService<ImporterApp>();
                return await app.RunAsync();
            }
        }
        private static void ConfigureServices(ServiceCollection services)
        {
            services.AddTransient<ImporterApp>();

            services.AddSingleton<IConfiguration>(s => _configuration);

            services.AddLogging(c => c.AddConfiguration(_configuration.GetSection("Logging")).AddConsole());

            services.AddTransient<ApiClient>(s => new ApiClient(s.GetService<ILogger<ApiClient>>(), s.GetService<IConfiguration>()));
        }
    }
}
