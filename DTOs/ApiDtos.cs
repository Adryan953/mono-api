using System;

namespace Mono.Api.DTOs
{
    // Auth DTOs
    public class RegisterRequestDto
    {
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Telefone { get; set; }
    }

    public class LoginRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
    }

    // Transaction DTOs
    public class CreateTransactionDto
    {
        public string Tipo { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public DateTime Data { get; set; }
        public string StatusPago { get; set; } = "settled";
        public Guid WalletId { get; set; }
    }

    public class TransactionResponseDto : CreateTransactionDto
    {
        public Guid Id { get; set; }
    }

    // Webhook DTO
    public class WhatsAppWebhookDto
    {
        public string From { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsAudio { get; set; }
        public string AudioUrl { get; set; } = string.Empty;
    }
}
