using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mono.Api.Data;
using Mono.Api.Entities;
using Mono.Api.Services;
using System.Text.Json;
using System.Text;

namespace Mono.Api.Controllers
{
    [ApiController]
    [Route("api/whatsapp")]
    public class WhatsAppController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IGeminiService _geminiService;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        private const string ZApiUrl = "https://api.z-api.io/instances/3F732485F93510400A209E26A2DA2CB2/token/80BE2506F43D83D766850D02/send-text";

        public WhatsAppController(AppDbContext context, IGeminiService geminiService, HttpClient httpClient, IConfiguration configuration)
        {
            _context = context;
            _geminiService = geminiService;
            _httpClient = httpClient;
            _configuration = configuration;
        }

        [HttpPost("receive")]
        public async Task<IActionResult> ReceiveWebhook([FromBody] JsonElement payload)
        {
            try
            {
                // ── 1. Extrair telefone do payload da Z-API ──────────────────────
                if (!payload.TryGetProperty("phone", out var phoneProp) ||
                    string.IsNullOrEmpty(phoneProp.GetString()))
                {
                    return BadRequest("Campo 'phone' ausente no payload.");
                }
                string phone = phoneProp.GetString()!;

                // ── 2. Extrair texto e/ou imagem ─────────────────────────────────

                // ── 2. Extrair texto e/ou imagem ─────────────────────────────────
                string messageText = string.Empty;
                if (payload.TryGetProperty("text", out var textProp) &&
                    textProp.TryGetProperty("message", out var msgProp))
                {
                    messageText = msgProp.GetString() ?? "";
                }

                string imageUrl = string.Empty;
                if (payload.TryGetProperty("image", out var imageProp) &&
                    imageProp.TryGetProperty("imageUrl", out var urlProp))
                {
                    imageUrl = urlProp.GetString() ?? "";
                }

                string audioUrl = string.Empty;
                string audioMimeType = "audio/ogg";
                if (payload.TryGetProperty("audio", out var audioProp) &&
                    audioProp.TryGetProperty("audioUrl", out var aUrlProp))
                {
                    audioUrl = aUrlProp.GetString() ?? "";
                    if (audioProp.TryGetProperty("mimeType", out var mimeProp))
                    {
                        var mime = mimeProp.GetString();
                        if (!string.IsNullOrEmpty(mime)) audioMimeType = mime;
                    }
                }

                // Ignorar payloads sem conteúdo (ex: acks de leitura, status)
                if (string.IsNullOrEmpty(messageText) && string.IsNullOrEmpty(imageUrl) && string.IsNullOrEmpty(audioUrl))
                {
                    return Ok(new { skipped = true, reason = "Payload sem texto, imagem ou áudio." });
                }

                // ── 3. Buscar usuário pelo telefone (normalizado sem 9º dígito) ──
                var usersList = await _context.Users.Where(u => u.Telefone != null).ToListAsync();

                var incoming = OnlyDigits(phone);
                if (incoming.StartsWith("55")) incoming = incoming.Substring(2);
                
                var incomingSuffix = incoming.Length >= 8 ? incoming.Substring(incoming.Length - 8) : incoming;
                var incomingDdd = incoming.Length >= 10 ? incoming.Substring(0, 2) : "";

                var user = usersList.FirstOrDefault(u => 
                {
                    var dbPhone = OnlyDigits(u.Telefone);
                    if (dbPhone.StartsWith("55")) dbPhone = dbPhone.Substring(2);
                    
                    var dbSuffix = dbPhone.Length >= 8 ? dbPhone.Substring(dbPhone.Length - 8) : dbPhone;
                    var dbDdd = dbPhone.Length >= 10 ? dbPhone.Substring(0, 2) : "";

                    return dbSuffix == incomingSuffix && dbDdd == incomingDdd;
                });

                Console.WriteLine($"[DEBUG] Telefone Z-API: {phone} | Sanitizado: {incoming} | Encontrado: {user != null}");

                if (user == null)
                {
                    // Usuário não cadastrado → informar via Z-API e encerrar
                    await SendZApiMessage(phone,
                        "⚠️ Seu número ainda não está cadastrado no Mono.\n" +
                        "Acesse https://app.mono.finance e crie sua conta para começar a registrar transações via WhatsApp! 🚀");

                    return Ok(new { success = false, reason = "Usuário não encontrado." });
                }

                // ── 4. Processar com Gemini ──────────────────────────────────────
                string geminiJsonResponse;

                if (!string.IsNullOrEmpty(audioUrl))
                {
                    var audioBytes = await _httpClient.GetByteArrayAsync(audioUrl);
                    geminiJsonResponse = await _geminiService.ParseTransactionFromAudioAsync(audioBytes, audioMimeType);
                }
                else if (!string.IsNullOrEmpty(imageUrl))
                {
                    var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);
                    geminiJsonResponse = await _geminiService.ParseTransactionFromReceiptAsync(imageBytes);
                }
                else
                {
                    geminiJsonResponse = await _geminiService.ParseTransactionFromTextAsync(messageText);
                }

                // Parse do JSON retornado pela IA
                GeminiTransactionResponse? aiData = null;
                try
                {
                    aiData = JsonSerializer.Deserialize<GeminiTransactionResponse>(geminiJsonResponse,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch
                {
                    // IA retornou algo fora do schema → log e fallback
                    Console.WriteLine($"[Gemini] Resposta inesperada: {geminiJsonResponse}");
                }

                // Fallback caso a IA não consiga extrair dados
                if (aiData == null || aiData.valor <= 0)
                {
                    await SendZApiMessage(phone,
                        "🤔 Não consegui entender a transação. Tente ser mais específico!\n" +
                        "Exemplo: _\"Almoço 35 reais\"_ ou envie a foto do comprovante PIX.");
                    return Ok(new { success = false, reason = "Dados insuficientes retornados pela IA." });
                }

                // Buscar a carteira padrão (primeira carteira criada/vinculada ao usuário)
                var defaultWallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == user.Id);

                if (defaultWallet == null)
                {
                    defaultWallet = new Wallet
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        Nome = "Carteira Principal",
                        Tipo = "Dinheiro",
                        SaldoInicial = 0.00m,
                        SaldoAtual = 0.00m
                    };
                    _context.Wallets.Add(defaultWallet);
                    await _context.SaveChangesAsync();
                }

                // ── 5. Salvar transação vinculada ao UserId e WalletId corretos ──────────────
                var transaction = new Transaction
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    WalletId = defaultWallet.Id,
                    Tipo = aiData.tipo?.Trim().ToLower() == "despesa" ? "Despesa" : "Receita",
                    Valor = aiData.valor,
                    Categoria = aiData.categoria ?? "Outros",
                    Descricao = aiData.descricao ?? "Registro via WhatsApp",
                    
                    // Força fuso horário do Brasil (-3h) no banco com Kind Utc para evitar Exception no EF/Npgsql
                    Data = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-3), DateTimeKind.Utc),
                    
                    // Visibilidade e status padrão ativos
                    StatusPago = "settled",
                    IsActive = true
                };

                _context.Transactions.Add(transaction);

                // ── 6. Atualizar Saldo da Carteira ─────────────────────────────────
                if (transaction.StatusPago == "settled")
                {
                    if (transaction.Tipo.ToLower() == "despesa")
                    {
                        defaultWallet.SaldoAtual -= transaction.Valor;
                    }
                    else if (transaction.Tipo.ToLower() == "receita")
                    {
                        defaultWallet.SaldoAtual += transaction.Valor;
                    }
                }

                await _context.SaveChangesAsync();

                // ── 6. Confirmar ao usuário via Z-API ────────────────────────────
                string tipoEmoji = transaction.Tipo == "receita" ? "🟢 Receita" : "🔴 Despesa";
                string valor = transaction.Valor.ToString("C", new System.Globalization.CultureInfo("pt-BR"));
                string confirmacao =
                    $"✅ Lançado com sucesso!\n\n" +
                    $"{tipoEmoji}: {valor}\n" +
                    $"📂 Categoria: {transaction.Categoria}\n" +
                    $"📝 Descrição: {transaction.Descricao}";

                await SendZApiMessage(phone, confirmacao);

                return Ok(new { success = true, transactionId = transaction.Id });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WhatsApp Webhook] Erro: {ex.Message}");
                // Retorna 200 OK para evitar que a Z-API fique enviando o webhook em loop infinito
                return Ok(new { success = false, error = ex.Message });
            }
        }

        // ── Helper: Envio de mensagem pela Z-API ────────────────────────────────
        private async Task SendZApiMessage(string phone, string message)
        {
            var formattedPhone = OnlyDigits(phone);
            if (!formattedPhone.StartsWith("55"))
            {
                formattedPhone = "55" + formattedPhone;
            }

            var zApiPayload = new { phone = formattedPhone, message };
            var content = new StringContent(
                JsonSerializer.Serialize(zApiPayload),
                Encoding.UTF8,
                "application/json");

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, ZApiUrl)
                {
                    Content = content
                };

                var response = await _httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[Z-API Error] Status: {response.StatusCode} - Body: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Z-API] Falha ao enviar mensagem para {formattedPhone}: {ex.Message}");
            }
        }

        // ── Helper: Normaliza telefone extraindo apenas os números ───────────────
        private static string OnlyDigits(string? raw)
        {
            return new string((raw ?? "").Where(char.IsDigit).ToArray());
        }
    }

    public class GeminiTransactionResponse
    {
        public decimal valor { get; set; }
        public string tipo { get; set; } = string.Empty;
        public string categoria { get; set; } = string.Empty;
        public string descricao { get; set; } = string.Empty;
    }
}
