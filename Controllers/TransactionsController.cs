using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Mono.Api.Data;
using Mono.Api.DTOs;
using Mono.Api.Entities;

namespace Mono.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TransactionsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TransactionsController(AppDbContext context)
        {
            _context = context;
        }

        private Guid GetUserId()
        {
            var subClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.Parse(subClaim ?? Guid.Empty.ToString());
        }

        private async Task<Guid> GetAccountOwnerIdAsync()
        {
            var userId = GetUserId();
            var user = await _context.Users.FindAsync(userId);
            return user?.ParentUserId ?? userId;
        }

        [HttpGet]
        public async Task<IActionResult> GetTransactions([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var accountId = await GetAccountOwnerIdAsync();
            var query = _context.Transactions
                .Where(t => t.UserId == accountId && t.IsActive);

            if (startDate.HasValue)
                query = query.Where(t => t.Data >= startDate.Value);
            
            if (endDate.HasValue)
                query = query.Where(t => t.Data <= endDate.Value);

            var transactions = await query
                .OrderByDescending(t => t.Data)
                .Select(t => new TransactionResponseDto
                {
                    Id = t.Id,
                    Valor = t.Valor,
                    Categoria = t.Categoria,
                    Data = t.Data,
                    Descricao = t.Descricao,
                    StatusPago = t.StatusPago,
                    Tipo = t.Tipo
                })
                .ToListAsync();

            return Ok(transactions);
        }

        [HttpGet("balance")]
        public async Task<IActionResult> GetBalance()
        {
            var accountId = await GetAccountOwnerIdAsync();
            var transactions = await _context.Transactions
                .Where(t => t.UserId == accountId && t.IsActive && t.StatusPago == "settled")
                .ToListAsync();

            var receitas = transactions.Where(t => t.Tipo.ToLower() == "receita" || t.Tipo.ToLower() == "income").Sum(t => t.Valor);
            var despesas = transactions.Where(t => t.Tipo.ToLower() == "despesa" || t.Tipo.ToLower() == "expense").Sum(t => t.Valor);
            var saldo = receitas - despesas;

            return Ok(new { balance = saldo });
        }

        [HttpPost]
        public async Task<IActionResult> CreateTransaction(CreateTransactionDto dto)
        {
            var accountId = await GetAccountOwnerIdAsync();
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = accountId,
                Valor = dto.Valor,
                Categoria = dto.Categoria,
                Data = dto.Data,
                Descricao = dto.Descricao,
                StatusPago = dto.StatusPago,
                Tipo = dto.Tipo?.Trim().ToLower() == "despesa" ? "Despesa" : "Receita",
                WalletId = dto.WalletId,
                IsActive = true
            };

            _context.Transactions.Add(transaction);

            // Atualizar saldo da carteira
            var wallet = await _context.Wallets.FindAsync(dto.WalletId);
            if (wallet != null && transaction.StatusPago == "settled")
            {
                if (transaction.Tipo.ToLower() == "despesa") wallet.SaldoAtual -= transaction.Valor;
                else if (transaction.Tipo.ToLower() == "receita") wallet.SaldoAtual += transaction.Valor;
            }

            await _context.SaveChangesAsync();

            var responseDto = new TransactionResponseDto
            {
                Id = transaction.Id,
                Valor = transaction.Valor,
                Categoria = transaction.Categoria,
                Data = transaction.Data,
                Descricao = transaction.Descricao,
                StatusPago = transaction.StatusPago,
                Tipo = transaction.Tipo
            };

            return CreatedAtAction(nameof(GetTransactions), new { id = transaction.Id }, responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTransaction(Guid id)
        {
            try
            {
                // 1. Extrai o ID do usuário logado (dono ou pai)
                var accountId = await GetAccountOwnerIdAsync();

                // 2. Busca a transação apenas pelo ID (Guid)
                var transaction = await _context.Transactions
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (transaction == null)
                {
                    return NotFound(new { message = "Transação não encontrada." });
                }

                // 3. Validação de segurança
                if (transaction.UserId != accountId)
                {
                    return Forbid(); // Retorna 403 se a transação não for desse usuário/conta
                }

                // 4. Exclui logicamente (Soft Delete)
                transaction.IsActive = false;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Transação excluída com sucesso." });
            }
            catch (Exception ex)
            {
                // Log detalhado no terminal da aplicação .NET
                Console.WriteLine($"[ERRO CRÍTICO DELETE]: {ex.Message}");
                var innerMessage = ex.InnerException != null ? ex.InnerException.Message : "Nenhuma inner exception";
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[INNER EXCEPTION]: {innerMessage}");
                }

                // Retorna os logs completos no JSON para visualização no DevTools do navegador
                return StatusCode(500, new { 
                    message = "Erro interno ao excluir transação.", 
                    details = ex.Message,
                    innerDetails = innerMessage,
                    stackTrace = ex.StackTrace
                });
            }
        }
    }
}
