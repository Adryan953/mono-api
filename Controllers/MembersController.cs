using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mono.Api.Data;
using Mono.Api.Entities;
using System.Security.Cryptography;
using System.Text;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Mono.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MembersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MembersController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetMembers([FromQuery] Guid accountOwnerId)
        {
            if (accountOwnerId == Guid.Empty)
            {
                return BadRequest("Owner ID is required.");
            }

            // Fetch the owner and their members
            var users = await _context.Users
                .Where(u => u.Id == accountOwnerId || u.ParentUserId == accountOwnerId)
                .Select(u => new
                {
                    u.Id,
                    u.Nome,
                    u.Email,
                    u.Role,
                    Status = "Ativo",
                    IsOwner = u.Id == accountOwnerId
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPost]
        public async Task<IActionResult> AddMember([FromBody] AddMemberRequest request)
        {
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                return BadRequest("Email já cadastrado.");
            }

            var owner = await _context.Users.FindAsync(request.AccountOwnerId);
            if (owner == null)
            {
                return NotFound("Conta principal não encontrada.");
            }

            var member = new User
            {
                Id = Guid.NewGuid(),
                Nome = request.Nome,
                Email = request.Email,
                ParentUserId = request.AccountOwnerId,
                Role = request.Role,
                SenhaHash = HashPassword(request.Senha)
            };

            _context.Users.Add(member);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Membro adicionado com sucesso." });
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }

    public class AddMemberRequest
    {
        public Guid AccountOwnerId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Senha { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
