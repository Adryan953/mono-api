using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mono.Api.Data;
using Mono.Api.Entities;
using System.Security.Claims;

namespace Mono.Api.Controllers
{
    [ApiController]
    [Route("api/wallets")]
    [Authorize]
    public class WalletsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WalletsController(AppDbContext context)
        {
            _context = context;
        }

        private Guid? GetParentOrUserId()
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(sub, out var id)) return null;

            // Para compartilhar, usamos o ParentUserId se existir, senão o próprio Id
            var user = _context.Users.Find(id);
            if (user == null) return null;

            return user.ParentUserId ?? user.Id;
        }

        [HttpGet]
        public async Task<IActionResult> GetWallets()
        {
            var ownerId = GetParentOrUserId();
            if (ownerId == null) return Unauthorized();

            var wallets = await _context.Wallets
                .Where(w => w.UserId == ownerId)
                .OrderBy(w => w.Nome)
                .ToListAsync();

            return Ok(wallets);
        }

        [HttpPost]
        public async Task<IActionResult> CreateWallet([FromBody] WalletDto dto)
        {
            var ownerId = GetParentOrUserId();
            if (ownerId == null) return Unauthorized();

            var ownerUser = await _context.Users.FindAsync(ownerId.Value);
            if (ownerUser == null) return Unauthorized();

            // Validação de Limite de Plano
            string plan = (ownerUser.Plano ?? "Basic").ToUpper();
            int limit = plan switch
            {
                string p when p.Contains("PREMIUM") => int.MaxValue,
                string p when p.Contains("PRO") => 5,
                _ => 3
            };

            var count = await _context.Wallets.CountAsync(w => w.UserId == ownerId.Value);
            if (count >= limit)
            {
                return BadRequest(new { message = "Limite de carteiras atingido para o seu plano atual." });
            }

            var wallet = new Wallet
            {
                UserId = ownerId.Value,
                Nome = dto.Nome,
                Tipo = dto.Tipo,
                SaldoInicial = dto.SaldoInicial,
                SaldoAtual = dto.SaldoInicial
            };

            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync();
            return Ok(wallet);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteWallet(Guid id)
        {
            var ownerId = GetParentOrUserId();
            if (ownerId == null) return Unauthorized();

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Id == id && w.UserId == ownerId);
            if (wallet == null) return NotFound();

            _context.Wallets.Remove(wallet);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    public class WalletDto
    {
        public string Nome { get; set; } = string.Empty;
        public string Tipo { get; set; } = "Corrente";
        public decimal SaldoInicial { get; set; }
    }
}
