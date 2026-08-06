using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Mono.Api.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;

        public GeminiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["GeminiSettings:ApiKey"] ?? string.Empty;
            _model = configuration["GeminiSettings:Model"] ?? "gemini-flash-latest";
        }

        public async Task<string> ParseTransactionFromTextAsync(string userMessage)
        {
            if (string.IsNullOrEmpty(_apiKey))
                throw new Exception("Gemini API Key is not configured.");

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("X-goog-api-key", _apiKey);

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = "Você é um assistente financeiro. Extraia a transação da seguinte mensagem. Retorne EXCLUSIVAMENTE em JSON (sem markdown blocks) no formato: { \"valor\": 35.00, \"tipo\": \"Despesa\", \"categoria\": \"Alimentação\", \"descricao\": \"Almoço\" }. Mensagem do usuário: " + userMessage }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json"
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if ((int)response.StatusCode == 429)
            {
                await Task.Delay(2000);
                request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("X-goog-api-key", _apiKey);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                response = await _httpClient.SendAsync(request);
            }
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            
            var textResult = doc.RootElement.GetProperty("candidates")[0]
                                .GetProperty("content").GetProperty("parts")[0]
                                .GetProperty("text").GetString();
                                
            return textResult ?? "{}";
        }

        public async Task<string> ParseTransactionFromReceiptAsync(byte[] imageBytes)
        {
            if (string.IsNullOrEmpty(_apiKey))
                throw new Exception("Gemini API Key is not configured.");

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("X-goog-api-key", _apiKey);

            var base64Image = Convert.ToBase64String(imageBytes);

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = "Extraia o valor total, data e destinatário do comprovante PIX. Retorne EXCLUSIVAMENTE em JSON (sem markdown blocks) no formato: { \"valor\": 150.00, \"tipo\": \"Despesa\", \"categoria\": \"Transferência\", \"descricao\": \"PIX Destinatário\" }." },
                            new 
                            {
                                inlineData = new 
                                {
                                    mimeType = "image/jpeg",
                                    data = base64Image
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json"
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if ((int)response.StatusCode == 429)
            {
                await Task.Delay(2000);
                request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("X-goog-api-key", _apiKey);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                response = await _httpClient.SendAsync(request);
            }
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            
            var textResult = doc.RootElement.GetProperty("candidates")[0]
                                .GetProperty("content").GetProperty("parts")[0]
                                .GetProperty("text").GetString();
                                
            return textResult ?? "{}";
        }

        public async Task<string> ParseTransactionFromAudioAsync(byte[] audioBytes, string mimeType)
        {
            if (string.IsNullOrEmpty(_apiKey))
                throw new Exception("Gemini API Key is not configured.");

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("X-goog-api-key", _apiKey);

            var base64Audio = Convert.ToBase64String(audioBytes);

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = "Ouça o áudio e extraia a transação. Retorne EXCLUSIVAMENTE em JSON (sem markdown blocks) no formato: { \"valor\": 35.00, \"tipo\": \"Despesa\", \"categoria\": \"Alimentação\", \"descricao\": \"Almoço\" }." },
                            new 
                            {
                                inlineData = new 
                                {
                                    mimeType = mimeType,
                                    data = base64Audio
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json"
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if ((int)response.StatusCode == 429)
            {
                await Task.Delay(2000);
                request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("X-goog-api-key", _apiKey);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                response = await _httpClient.SendAsync(request);
            }
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            
            var textResult = doc.RootElement.GetProperty("candidates")[0]
                                .GetProperty("content").GetProperty("parts")[0]
                                .GetProperty("text").GetString();
                                
            return textResult ?? "{}";
        }
    }
}
