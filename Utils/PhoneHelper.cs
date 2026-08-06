using System.Linq;

namespace Mono.Api.Utils
{
    public static class PhoneHelper
    {
        public static string? Sanitize(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;

            // 1. Remove todos os caracteres não numéricos.
            var digits = new string(raw.Where(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(digits)) return digits;

            // 2. Se o número começar com 55, remova temporariamente.
            // A regra dizia 'se começar com 55 e tiver 13 dígitos', mas para sermos seguros
            // e atingirmos o formato DDD + 9 ou 8 dígitos, removemos o 55 inicial se existir.
            if (digits.StartsWith("55"))
            {
                digits = digits.Substring(2);
            }

            // 3. Se o número tiver 11 dígitos (DDD + 9 dígitos) e o terceiro dígito for 9, remova-o.
            if (digits.Length == 11 && digits[2] == '9')
            {
                digits = digits.Remove(2, 1);
            }

            // 4. Adicione o prefixo 55.
            return "55" + digits;
        }
    }
}
