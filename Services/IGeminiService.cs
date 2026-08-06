using System.Threading.Tasks;

namespace Mono.Api.Services
{
    public interface IGeminiService
    {
        Task<string> ParseTransactionFromTextAsync(string userMessage);
        Task<string> ParseTransactionFromReceiptAsync(byte[] imageBytes);
    }
}
