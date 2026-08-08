using System;
using System.Collections.Generic;

namespace Mono.Api.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SenhaHash { get; set; } = string.Empty;
        public string? Telefone { get; set; }
        public string Plano { get; set; } = "Individual"; // Individual, Familia, Business, PRO, Basic
        public bool PlanoAtivo { get; set; } = false;
        public DateTime? DataExpiracaoPlano { get; set; }
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        public Guid? ParentUserId { get; set; }
        public string Role { get; set; } = "Admin"; // Admin, Lancador, Visualizador

        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
