using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mono.Api.Data;
using System.Security.Claims;

namespace Mono.Api.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReportsController(AppDbContext context)
        {
            _context = context;
        }

        private Guid? GetUserId()
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? id : null;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] string period = "month")
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            DateTime startDate = DateTime.UtcNow;
            if (period == "month") startDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            else if (period == "trimestre") startDate = DateTime.UtcNow.AddMonths(-3);
            else if (period == "year") startDate = new DateTime(DateTime.UtcNow.Year, 1, 1);
            else startDate = DateTime.UtcNow.AddYears(-10); // All time or custom default

            var transactions = await _context.Transactions
                .Where(t => t.UserId == userId && t.IsActive && t.Data >= startDate)
                .ToListAsync();

            var receitas = transactions.Where(t => t.Tipo.ToLower() == "receita").Sum(t => t.Valor);
            var despesas = transactions.Where(t => t.Tipo.ToLower() == "despesa").Sum(t => t.Valor);
            var saldo = receitas - despesas;
            
            decimal taxaPoupanca = 0;
            if (receitas > 0)
            {
                taxaPoupanca = (saldo / receitas) * 100;
            }

            // Evolução por mês
            var evolucao = transactions
                .GroupBy(t => new { t.Data.Year, t.Data.Month })
                .Select(g => new
                {
                    Mes = $"{g.Key.Year}-{g.Key.Month:D2}",
                    Receitas = g.Where(t => t.Tipo.ToLower() == "receita").Sum(t => t.Valor),
                    Despesas = g.Where(t => t.Tipo.ToLower() == "despesa").Sum(t => t.Valor),
                    Saldo = g.Where(t => t.Tipo.ToLower() == "receita").Sum(t => t.Valor) - g.Where(t => t.Tipo.ToLower() == "despesa").Sum(t => t.Valor)
                })
                .OrderBy(x => x.Mes)
                .ToList();

            return Ok(new { receitas, despesas, saldo, taxaPoupanca, evolucao });
        }

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories([FromQuery] string period = "month")
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            DateTime startDate = DateTime.UtcNow;
            if (period == "month") startDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            else if (period == "trimestre") startDate = DateTime.UtcNow.AddMonths(-3);
            else if (period == "year") startDate = new DateTime(DateTime.UtcNow.Year, 1, 1);
            else startDate = DateTime.UtcNow.AddYears(-10);

            var despesasPorCategoria = await _context.Transactions
                .Where(t => t.UserId == userId && t.IsActive && t.Tipo.ToLower() == "despesa" && t.Data >= startDate)
                .GroupBy(t => t.Categoria)
                .Select(g => new
                {
                    Categoria = string.IsNullOrEmpty(g.Key) ? "Outros" : g.Key,
                    Total = g.Sum(t => t.Valor)
                })
                .OrderByDescending(x => x.Total)
                .ToListAsync();

            return Ok(despesasPorCategoria);
        }

        [HttpGet("dre")]
        public async Task<IActionResult> GetDre([FromQuery] string period = "month")
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null || user.Plano != "Business")
            {
                return StatusCode(403, "O Relatório DRE é exclusivo para planos Business.");
            }

            DateTime startDate = DateTime.UtcNow;
            if (period == "month") startDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            else if (period == "trimestre") startDate = DateTime.UtcNow.AddMonths(-3);
            else if (period == "year") startDate = new DateTime(DateTime.UtcNow.Year, 1, 1);
            else startDate = DateTime.UtcNow.AddYears(-10);

            var transactions = await _context.Transactions
                .Where(t => t.UserId == userId && t.IsActive && t.Data >= startDate)
                .ToListAsync();

            var receitaBruta = transactions.Where(t => t.Tipo.ToLower() == "receita").Sum(t => t.Valor);
            var custosVariaveis = transactions.Where(t => t.Tipo.ToLower() == "despesa" && (t.Categoria == "Custos" || t.Categoria == "Variável")).Sum(t => t.Valor);
            var margemContribuicao = receitaBruta - custosVariaveis;
            var despesasFixas = transactions.Where(t => t.Tipo.ToLower() == "despesa" && t.Categoria != "Custos" && t.Categoria != "Variável").Sum(t => t.Valor);
            var lucroLiquido = margemContribuicao - despesasFixas;

            return Ok(new
            {
                ReceitaBruta = receitaBruta,
                CustosVariaveis = custosVariaveis,
                MargemContribuicao = margemContribuicao,
                DespesasFixas = despesasFixas,
                LucroLiquido = lucroLiquido
            });
        }
    }
}
