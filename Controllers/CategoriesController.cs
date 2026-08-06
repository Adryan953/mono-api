using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mono.Api.Data;
using Mono.Api.Entities;
using System.Security.Claims;

namespace Mono.Api.Controllers
{
    [ApiController]
    [Route("api/categories")]
    [Authorize]
    public class CategoriesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CategoriesController(AppDbContext context)
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
        public async Task<IActionResult> GetCategories()
        {
            var ownerId = GetParentOrUserId();
            if (ownerId == null) return Unauthorized();

            var categories = await _context.Categories
                .Where(c => c.UserId == ownerId)
                .OrderBy(c => c.Nome)
                .ToListAsync();

            return Ok(categories);
        }

        [HttpPost]
        public async Task<IActionResult> CreateCategory([FromBody] CategoryDto dto)
        {
            var ownerId = GetParentOrUserId();
            if (ownerId == null) return Unauthorized();

            var category = new Category
            {
                UserId = ownerId.Value,
                Nome = dto.Nome,
                Tipo = dto.Tipo,
                Icone = dto.Icone,
                Cor = dto.Cor
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return Ok(category);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(Guid id)
        {
            var ownerId = GetParentOrUserId();
            if (ownerId == null) return Unauthorized();

            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == ownerId);
            if (category == null) return NotFound();

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] CategoryDto dto)
        {
            var ownerId = GetParentOrUserId();
            if (ownerId == null) return Unauthorized();

            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == ownerId);
            if (category == null) return NotFound();

            category.Nome = dto.Nome;
            category.Tipo = dto.Tipo;
            category.Icone = dto.Icone;
            category.Cor = dto.Cor;

            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
            
            return Ok(category);
        }
    }

    public class CategoryDto
    {
        public string Nome { get; set; } = string.Empty;
        public string Tipo { get; set; } = "expense";
        public string Icone { get; set; } = "fa-tags";
        public string Cor { get; set; } = "#64748b";
    }
}
