using System;

namespace Mono.Api.Entities
{
    public class Card
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; } // Owner
        public string Nome { get; set; } = string.Empty;
        public string Bandeira { get; set; } = "Mastercard";
        public decimal LimiteTotal { get; set; } = 0;
        public int DiaFechamento { get; set; } = 1;
        public int DiaVencimento { get; set; } = 5;
        public string Cor { get; set; } = "#3b82f6"; // Default blue
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
    }
}
