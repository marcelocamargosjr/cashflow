using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Cashflow.Ledger.Application.Abstractions.Idempotency;

public static class RequestCanonicalizer
{
    public static string Hash(IdempotencyCanonicalDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{dto.Operation}|{dto.MerchantId:N}|{dto.IdempotencyKey:N}|{dto.Body}");

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
