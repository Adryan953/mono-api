using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mono.Api.Data;
using Mono.Api.Entities;
using System.Security.Claims;

namespace Mono.Api.Controllers
{
    [ApiController]
    [Route("api/cards")]
    [Authorize]
    public class CardsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CardsController(AppDbContext context)
        {
            _context = context;
        }

        private Guid? GetParentOrUserId()
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(sub, out var id)) return null;

            var user = _context.Users.Find(id);
            if (user == null) return null;

            return user.ParentUserId ?? user.Id;
        }

        [HttpGet]
        public async Task<IActionResult> GetCards()
        {
            var ownerId = GetParentOrUserId();
            if (ownerId == null) return Unauthorized();

            var cards = await _context.Cards
                .Where(c => c.UserId == ownerId)
                .OrderBy(c => c.Nome)
                .ToListAsync();

            return Ok(cards);
        }

        [HttpPost]
        public async Task<IActionResult> CreateCard([FromBody] CardDto dto)
        {
            var ownerId = GetParentOrUserId();
            if (ownerId == null) return Unauthorized();

            var card = new Card
            {
                UserId = ownerId.Value,
                Nome = dto.Nome,
                Bandeira = dto.Bandeira,
                LimiteTotal = dto.LimiteTotal,
                DiaFechamento = dto.DiaFechamento,
                DiaVencimento = dto.DiaVencimento,
                Cor = dto.Cor
            };

            _context.Cards.Add(card);
            await _context.SaveChangesAsync();
            return Ok(card);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCard(Guid id)
        {
            var ownerId = GetParentOrUserId();
            if (ownerId == null) return Unauthorized();

            var card = await _context.Cards.FirstOrDefaultAsync(c => c.Id == id && c.UserId == ownerId);
            if (card == null) return NotFound();

            _context.Cards.Remove(card);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    public class CardDto
    {
        public string Nome { get; set; } = string.Empty;
        public string Bandeira { get; set; } = "Mastercard";
        public decimal LimiteTotal { get; set; }
        public int DiaFechamento { get; set; } = 1;
        public int DiaVencimento { get; set; } = 5;
        public string Cor { get; set; } = "#3b82f6";
    }
}
