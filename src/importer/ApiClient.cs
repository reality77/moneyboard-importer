using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using System.Net;
using System.IO;
using System.Text;

namespace importer
{
    public class ApiClient
    {
        private readonly ILogger<ApiClient> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _client;
        
        public ApiClient(ILogger<ApiClient> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };

            _client = new HttpClient(httpClientHandler);
            _client.BaseAddress = new Uri(configuration.GetValue<string>("Api:Url"));
        }

        public async Task<T> GetAsync<T>(string url, bool setNullIf404 = true) 
        {
            var response = await _client.GetAsync(url);

            if(setNullIf404 && response.StatusCode == HttpStatusCode.NotFound)
                return default(T);

            if(!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"{url} => {response.StatusCode}");
                throw new ApiClientException($"{response.StatusCode} : {await response.Content.ReadAsStringAsync()}");
            }
            else
            {
                if(_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug($"{url} => {response.StatusCode}");
                }
            }

            var streamTask = response.Content.ReadAsStreamAsync();
            var result = await JsonSerializer.DeserializeAsync<T>(await streamTask, GetOptions()).ConfigureAwait(false);

            if(result == null)
            {
                _logger.LogDebug($"Serialization error on content : {await response.Content.ReadAsStringAsync()}");
                throw new ApiClientException("Serialization error");
            }
            else
                return result;
        }

        public async Task<T> PostAsync<T, U>(string url, U body) where U : class
        {
            return await PostAsync<T>(url, JsonSerializer.Serialize<U>(body), "application/json");            
        }

        public async Task<T> PostAsync<T>(string url, string jsonBody, string mediaType) 
        {
            var content = new StringContent(jsonBody, Encoding.UTF8, mediaType);
            var response = await _client.PostAsync(url, content);

            if(!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"{url} => {response.StatusCode}");
                throw new ApiClientException($"{response.StatusCode} : {await response.Content.ReadAsStringAsync()}");
            }
            else
            {
                if(_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug($"{url} => {response.StatusCode}");
                }
            }

            var streamTask = response.Content.ReadAsStreamAsync();
            var result = await JsonSerializer.DeserializeAsync<T>(await streamTask, GetOptions()).ConfigureAwait(false);

            if(result == null)
            {
                _logger.LogDebug($"Serialization error on content : {await response.Content.ReadAsStringAsync()}");
                throw new ApiClientException("Serialization error");
            }
            else
                return result;
        }

        public async Task<string> PostFileAsync(string url, string filePath)
        {
            var content = new MultipartFormDataContent();
            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

            content.Add(new StreamContent(stream), Path.GetFileName(filePath), Path.GetFileName(filePath));

            var response = await _client.PostAsync(url, content);

            if(!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"{url} => {response.StatusCode}");
                throw new ApiClientException($"{response.StatusCode} : {await response.Content.ReadAsStringAsync()}");
            }
            else
            {
                if(_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug($"{url} => {response.StatusCode}");
                }
            }

            return await response.Content.ReadAsStringAsync();
        }

        private JsonSerializerOptions GetOptions()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            options.PropertyNameCaseInsensitive = true;
            options.IgnoreNullValues = true;
            return options;
        }
    }

    [System.Serializable]
    public class ApiClientException : System.Exception
    {
        public ApiClientException() { }
        public ApiClientException(string message) : base(message) { }
        public ApiClientException(string message, System.Exception inner) : base(message, inner) { }
        protected ApiClientException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}