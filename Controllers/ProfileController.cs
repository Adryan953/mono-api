using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mono.Api.Data;
using System.Security.Claims;

namespace Mono.Api.Controllers
{
    [ApiController]
    [Route("api/user/profile")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProfileController(AppDbContext context)
        {
            _context = context;
        }

        private Guid? GetUserId()
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? id : null;
        }

        [HttpPut]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound("Usuário não encontrado.");

            if (!string.IsNullOrEmpty(request.Nome))
                user.Nome = request.Nome;
                
            if (!string.IsNullOrEmpty(request.Telefone))
                user.Telefone = Utils.PhoneHelper.Sanitize(request.Telefone);

            if (!string.IsNullOrEmpty(request.Email))
            {
                // Verifica se já existe outro usuário com o e-mail
                var emailExists = await _context.Users.AnyAsync(u => u.Email == request.Email && u.Id != userId);
                if (emailExists)
                    return BadRequest("Este e-mail já está em uso por outra conta.");
                
                user.Email = request.Email;
            }
            
            // Simulação de alteração de senha (precisaria injetar o IPasswordHasher se fossemos atualizar de verdade, ou um hash manual)
            if (!string.IsNullOrEmpty(request.NovaSenha) && request.NovaSenha == request.ConfirmarSenha)
            {
                user.SenhaHash = BCrypt.Net.BCrypt.HashPassword(request.NovaSenha);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Perfil atualizado com sucesso!", nome = user.Nome, email = user.Email, telefone = user.Telefone });
        }
    }

    public class UpdateProfileRequest
    {
        public string? Nome { get; set; }
        public string? Email { get; set; }
        public string? Telefone { get; set; }
        public string? SenhaAtual { get; set; }
        public string? NovaSenha { get; set; }
        public string? ConfirmarSenha { get; set; }
    }
}
