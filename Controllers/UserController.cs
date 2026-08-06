using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mono.Api.Data;
using Mono.Api.Entities;
using System.Security.Claims;
using System;
using System.Threading.Tasks;

namespace Mono.Api.Controllers
{
    [ApiController]
    [Route("api/user")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UserController(AppDbContext context)
        {
            _context = context;
        }

        private Guid? GetUserId()
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? id : null;
        }

        // GET /api/user/profile
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound("Perfil não encontrado.");

            return Ok(new
            {
                user.Id,
                user.Nome,
                user.Email,
                user.Telefone,
                user.Plano,
                user.Role,
                user.DataCriacao
            });
        }

        // PUT /api/user/profile
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UserUpdateProfileRequest request)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound("Usuário não encontrado.");

            if (!string.IsNullOrEmpty(request.Nome)) user.Nome = request.Nome;
            // Email usually shouldn't be updated or needs verification, but keeping it simple as requested
            // if (!string.IsNullOrEmpty(request.Email)) user.Email = request.Email;
            if (request.Telefone != null) user.Telefone = Utils.PhoneHelper.Sanitize(request.Telefone);

            if (!string.IsNullOrEmpty(request.NovaSenha))
            {
                if (string.IsNullOrEmpty(request.SenhaAtual))
                    return BadRequest("Para alterar a senha, informe a senha atual.");

                if (user.SenhaHash != HashPassword(request.SenhaAtual))
                    return BadRequest("Senha atual incorreta.");

                if (request.NovaSenha != request.ConfirmarSenha)
                    return BadRequest("A nova senha e a confirmação não coincidem.");

                user.SenhaHash = HashPassword(request.NovaSenha);
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Perfil atualizado com sucesso." });
        }

        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        // PUT /api/user/plan
        [HttpPut("plan")]
        public async Task<IActionResult> UpdatePlan([FromBody] UpdatePlanRequest request)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var planosValidos = new[] { "Individual", "Familia", "Business" };
            if (string.IsNullOrEmpty(request.Plano) || !Array.Exists(planosValidos, p => p == request.Plano))
            {
                return BadRequest($"Plano inválido. Use: {string.Join(", ", planosValidos)}");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound("Usuário não encontrado.");

            user.Plano = request.Plano;
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Plano atualizado com sucesso.", Plano = user.Plano });
        }
    }

    public class UpdatePlanRequest
    {
        public string Plano { get; set; } = string.Empty;
    }

    public class UserUpdateProfileRequest
    {
        public string? Nome { get; set; }
        public string? Email { get; set; }
        public string? Telefone { get; set; }
        public string? SenhaAtual { get; set; }
        public string? NovaSenha { get; set; }
        public string? ConfirmarSenha { get; set; }
    }
}
