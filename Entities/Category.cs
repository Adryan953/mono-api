using System;

namespace Mono.Api.Entities
{
    public class Category
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; } // Owner
        public string Nome { get; set; } = string.Empty;
        public string Tipo { get; set; } = "expense"; // income, expense
        public string Icone { get; set; } = "fa-tags";
        public string Cor { get; set; } = "#64748b";
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
    }
}
