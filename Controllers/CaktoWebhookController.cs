using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mono.Api.Data;
using System.Text.Json;
using System;
using System.Threading.Tasks;

namespace Mono.Api.Controllers
{
    [ApiController]
    [Route("api/webhooks/cakto")]
    public class CaktoWebhookController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public CaktoWebhookController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> HandleWebhook([FromBody] JsonElement payload)
        {
            try
            {
                // 1. Validação da Chave Secreta
                var configuredSecret = _configuration["CAKTO_WEBHOOK_SECRET"] ?? Environment.GetEnvironmentVariable("CAKTO_WEBHOOK_SECRET");
                if (!string.IsNullOrEmpty(configuredSecret))
                {
                    var requestSecret = Request.Headers["X-Cakto-Secret"].ToString();
                    if (string.IsNullOrEmpty(requestSecret))
                        requestSecret = Request.Headers["Authorization"].ToString().Replace("Bearer ", "").Trim();
                    if (string.IsNullOrEmpty(requestSecret))
                        requestSecret = Request.Query["secret"].ToString();
                    
                    if (requestSecret != configuredSecret)
                    {
                        return Unauthorized(new { success = false, message = "Chave de webhook inválida ou não fornecida." });
                    }
                }

                // 2. Extração de Evento e Status
                string? eventName = null;
                if (payload.TryGetProperty("event", out var eventElement))
                {
                    eventName = eventElement.GetString();
                }

                string? statusName = null;
                if (payload.TryGetProperty("status", out var statusElement))
                {
                    statusName = statusElement.GetString();
                }
                else if (payload.TryGetProperty("data", out var dataStatusElement) && dataStatusElement.ValueKind == JsonValueKind.Object)
                {
                    if (dataStatusElement.TryGetProperty("status", out var innerStatusElement))
                    {
                        statusName = innerStatusElement.GetString();
                    }
                }

                var action = (eventName ?? statusName ?? "").ToLower();

                // 3. Extração do E-mail do Cliente
                string? email = null;
                if (payload.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Object)
                {
                    if (dataElement.TryGetProperty("customer", out var customerElement) && customerElement.ValueKind == JsonValueKind.Object)
                    {
                        if (customerElement.TryGetProperty("email", out var emailElement))
                            email = emailElement.GetString();
                    }
                }
                
                if (string.IsNullOrEmpty(email) && payload.TryGetProperty("customer", out var customerTop) && customerTop.ValueKind == JsonValueKind.Object)
                {
                    if (customerTop.TryGetProperty("email", out var emailElement))
                        email = emailElement.GetString();
                }

                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(action))
                {
                    // Falta e-mail ou ação
                    return Ok(new { success = true, message = "Evento ignorado, dados insuficientes (e-mail ou status ausentes)." });
                }

                // 4. Busca do Usuário no PostgreSQL
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    return Ok(new { success = true, message = "Usuário não encontrado no banco de dados." });
                }

                // 5. Processamento do Status / Evento
                var isApproved = action == "paid" || action == "approved" || 
                                 action == "subscription.approved" || action == "purchase.approved";

                var isCanceled = action == "canceled" || action == "refunded" || 
                                 action == "subscription.canceled" || action == "purchase.refunded" || 
                                 action == "subscription.expired";

                if (isApproved)
                {
                    user.Plano = "PRO"; 
                    user.PlanoAtivo = true;
                    // Define validade baseada no evento. Ex: +30 dias. Em produção, extrair da data do webhook se disponível.
                    user.DataExpiracaoPlano = DateTime.UtcNow.AddMonths(1); 
                }
                else if (isCanceled)
                {
                    user.Plano = "Basic";
                    user.PlanoAtivo = false;
                    user.DataExpiracaoPlano = null;
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Webhook processado com sucesso!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro no processamento do webhook da Cakto: {ex.Message}");
                // Retorna 200 OK para evitar retries infinitos do provider de webhook
                return Ok(new { success = false, message = "Erro interno evitado." });
            }
        }
    }
}
