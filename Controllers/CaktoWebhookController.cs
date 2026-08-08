using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public CaktoWebhookController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> HandleWebhook([FromBody] JsonElement payload)
        {
            try
            {
                // Estrutura genérica assumida: { "event": "...", "data": { "customer": { "email": "..." } } }
                // Ou formato direto { "event": "...", "customer": { "email": "..." } }
                string? eventName = null;
                if (payload.TryGetProperty("event", out var eventElement))
                {
                    eventName = eventElement.GetString();
                }

                string? email = null;

                // Procura o e-mail em diferentes níveis do payload genérico
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

                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(eventName))
                {
                    // Falta e-mail ou evento, apenas retorna 200 para ack
                    return Ok(new { success = true, message = "Evento ignorado, dados insuficientes." });
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    return Ok(new { success = true, message = "Usuário não encontrado." });
                }

                if (eventName == "subscription.approved" || eventName == "purchase.approved")
                {
                    user.Plano = "PRO"; // Ajustar conforme a lógica real do produto, se houver
                    user.PlanoAtivo = true;
                    // Exemplo: Expira em 1 mês. Pode-se extrair do payload se houver "expires_at".
                    user.DataExpiracaoPlano = DateTime.UtcNow.AddMonths(1); 
                }
                else if (eventName == "subscription.canceled" || eventName == "purchase.refunded" || eventName == "subscription.expired")
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
