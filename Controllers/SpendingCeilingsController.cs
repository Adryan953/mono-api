using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mono.Api.Data;
using Mono.Api.Entities;
using System.Security.Claims;

namespace Mono.Api.Controllers
{
    [ApiController]
    [Route("api/spending-ceilings")]
    [Authorize]
    public class SpendingCeilingsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SpendingCeilingsController(AppDbContext context)
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
        public async Task<IActionResult> GetSpendingCeilings()
        {
            var ownerId = GetParentOrUserId();
            if (ownerId == null) return Unauthorized();

            var ceilings = await _context.SpendingCeilings
                .Where(c => c.UserId == ownerId)
                .ToListAsync();

            if (!ceilings.Any())
            {
                // Create default ceilings
                ceilings = new List<SpendingCeiling>
                {
                    new SpendingCeiling { UserId = ownerId.Value, CategoryName = "Essenciais", Percentage = 45m },
                    new SpendingCeiling { UserId = ownerId.Value, CategoryName = "Investimentos", Percentage = 20m },
                    new SpendingCeiling { UserId = ownerId.Value, CategoryName = "Lazer", Percentage = 20m },
                    new SpendingCeiling { UserId = ownerId.Value, CategoryName = "Conhecimento", Percentage = 10m },
                    new SpendingCeiling { UserId = ownerId.Value, CategoryName = "Filantropia", Percentage = 5m }
                };

                _context.SpendingCeilings.AddRange(ceilings);
                await _context.SaveChangesAsync();
            }

            return Ok(ceilings);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateSpendingCeilings([FromBody] List<SpendingCeilingItemDto> items)
        {
            var ownerId = GetParentOrUserId();
            if (ownerId == null) return Unauthorized();

            var existingCeilings = await _context.SpendingCeilings
                .Where(c => c.UserId == ownerId)
                .ToListAsync();

            _context.SpendingCeilings.RemoveRange(existingCeilings);

            var ceilingsToAdd = items.Select(dto => new SpendingCeiling
            {
                UserId = ownerId.Value,
                CategoryName = dto.Name,
                Percentage = dto.Percentage
            }).ToList();

            _context.SpendingCeilings.AddRange(ceilingsToAdd);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Tetos salvos com sucesso!" });
        }
    }

    public class SpendingCeilingItemDto 
    {
        public string Name { get; set; } = string.Empty;
        public decimal Percentage { get; set; }
    }
}
