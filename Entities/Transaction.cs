using System;

namespace Mono.Api.Entities
{
    public class Transaction
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Tipo { get; set; } = string.Empty; // 'receita' or 'despesa'
        public decimal Valor { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public DateTime Data { get; set; }
        public string StatusPago { get; set; } = "settled"; // 'settled' or 'pending'
        public bool IsActive { get; set; } = true;
        public Guid WalletId { get; set; } // Obrigatório agora

        public User? User { get; set; }
    }
}
