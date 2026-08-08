using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Mono.Api.Services
{
    public interface ICaktoService
    {
        Task<string?> GetAccessTokenAsync();
    }

    public class CaktoService : ICaktoService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _clientId;
        private readonly string? _clientSecret;
        private string? _cachedToken;
        private DateTime _tokenExpiration;

        public CaktoService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            // Assumindo a URL base da API da Cakto. Se for diferente, pode ser ajustado aqui ou no appsettings.json
            var baseUrl = configuration["Cakto:BaseUrl"] ?? "https://api.cakto.com.br";
            _httpClient.BaseAddress = new Uri(baseUrl); 
            _clientId = configuration["Cakto:ClientId"];
            _clientSecret = configuration["Cakto:ClientSecret"];
        }

        public async Task<string?> GetAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiration)
            {
                return _cachedToken;
            }

            if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
            {
                Console.WriteLine("Cakto ClientId ou ClientSecret não configurados.");
                return null;
            }

            try
            {
                var authBytes = Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}");
                var base64Auth = Convert.ToBase64String(authBytes);

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Auth);
                
                var content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");
                var response = await _httpClient.PostAsync("/oauth/token", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    using var document = JsonDocument.Parse(responseJson);
                    
                    if (document.RootElement.TryGetProperty("access_token", out var tokenElement))
                    {
                        _cachedToken = tokenElement.GetString();
                        
                        if (document.RootElement.TryGetProperty("expires_in", out var expiresInElement) && expiresInElement.TryGetInt32(out var expiresIn))
                        {
                            _tokenExpiration = DateTime.UtcNow.AddSeconds(expiresIn - 60); // 1 minuto de margem
                        }
                        else
                        {
                            _tokenExpiration = DateTime.UtcNow.AddHours(1); // fallback
                        }
                        return _cachedToken;
                    }
                }
                else
                {
                    Console.WriteLine($"Erro ao obter token Cakto: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exceção ao autenticar com Cakto: {ex.Message}");
            }

            return null;
        }
    }
}
