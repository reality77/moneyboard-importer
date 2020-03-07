using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace importer
{
    public class ImporterApp
    {
        private readonly ILogger<ImporterApp> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;

        public ImporterApp(ILogger<ImporterApp> logger, IConfiguration configuration, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
        }

        public async Task<int> RunAsync()
        {
            _logger.LogInformation("Starting importer app");

            var confApi = _configuration.GetSection("Api");
            var confSourceFiles = _configuration.GetSection("SourceFiles");

            var regexAccoutNo = new Regex(confSourceFiles.GetValue<string>("AccountNoRegex"));
            var sourcePath = confSourceFiles.GetValue<string>("Path");

            if(!Directory.Exists(sourcePath))
            {
                _logger.LogCritical($"The source path does not exists : {sourcePath}");
                return 64;
            }

            var dicExistingAccounts = new Dictionary<string, dto.Model.AccountDetails>();
            var client = CreateApiClient();

            foreach(var file in Directory.GetFiles(sourcePath))
            {
                var fileName = Path.GetFileName(file);
                var match = regexAccoutNo.Match(fileName);

                if(match.Success)
                {
                    var accountNo = match.Groups["accountNo"]?.Value;
                    
                    if(accountNo == null)
                        _logger.LogDebug($"File {fileName} skipped - no match (group)");
                    else
                    {
                        _logger.LogInformation($"Processing file {fileName}");

                        dto.Model.AccountDetails account = null;

                        if(!dicExistingAccounts.ContainsKey(accountNo))
                        {
                            account = await client.GetAsync<dto.Model.AccountDetails>($"accounts/by?number={WebUtility.UrlEncode(accountNo)}");
                            dicExistingAccounts.Add(accountNo, account);
                        }
                        else
                            account = dicExistingAccounts[accountNo];

                        if(account != null)
                        {
                            var result = await client.PostFileAsync($"import?accountId={account.Id}", file);
                            _logger.LogInformation(result);
                        }
                        else
                            _logger.LogWarning($"Account {accountNo} does not exists - skipped");

                    }
                }
                else
                    _logger.LogDebug($"File {fileName} skipped - no match");
            }

            return 0;
        }

        public ApiClient CreateApiClient()
        {
            return _serviceProvider.GetService<ApiClient>();
        }
    }
}