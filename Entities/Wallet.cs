using System;

namespace Mono.Api.Entities
{
    public class Wallet
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; } // The owner of the account
        public string Nome { get; set; } = string.Empty;
        public string Tipo { get; set; } = "Corrente"; // Corrente, Poupança, Investimento, Dinheiro Físico
        public decimal SaldoInicial { get; set; } = 0;
        public decimal SaldoAtual { get; set; } = 0;
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
    }
}
