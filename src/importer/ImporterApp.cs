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
using dto.Model;

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

            _logger.LogDebug($"Using connection {confApi.GetValue<string>("Url")}");
            _logger.LogDebug($"Listing Files in {sourcePath}");

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

                        AccountDetails account = null;

                        if(!dicExistingAccounts.ContainsKey(accountNo))
                        {
                            account = await client.GetAsync<AccountDetails>($"accounts/by?number={WebUtility.UrlEncode(accountNo)}");

                            if(account != null)
                                dicExistingAccounts.Add(accountNo, account);
                        }
                        else
                            account = dicExistingAccounts[accountNo];

                        if(account == null)
                        {
                            _logger.LogDebug($"No account {accountNo} detected - Creating account");

                            if(confApi.GetValue<bool>("AutoCreateAccounts"))
                            {
                                account = new AccountDetails
                                {
                                    Name = accountNo,
                                    Number = accountNo,
                                    InitialBalance = 0,
                                };

                                account = await client.PostAsync<AccountDetails, AccountDetails>("accounts", account);

                                dicExistingAccounts.Add(accountNo, account);
                                _logger.LogInformation($"Account #{account.Id} - {account.Number}");
                            }
                            else
                                _logger.LogWarning($"Account {accountNo} does not exists - skipped");
                        }

                        if(account != null)
                        {
                            _logger.LogDebug($"Importing data in account {account.Id}");
                            var result = await client.PostFileAsync($"import?accountId={account.Id}", file);
                            _logger.LogInformation(result);
                        }
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