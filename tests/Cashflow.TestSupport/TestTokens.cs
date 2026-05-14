using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Cashflow.TestSupport;

public static class TestTokens
{
    public const string Issuer = "https://cashflow-test/realm/cashflow";
    public const string Audience = "cashflow-api";

    public static readonly SymmetricSecurityKey SigningKey =
        new("cashflow-test-signing-key-do-not-use-in-prod-please-1234567890"u8.ToArray());

    public static string MerchantToken(Guid? merchantId = null, TimeSpan? lifetime = null)
        => Issue(merchantId ?? Guid.NewGuid(), roles: new[] { "merchant" }, lifetime);

    public static string AdminToken(TimeSpan? lifetime = null)
        => Issue(Guid.NewGuid(), roles: new[] { "admin" }, lifetime);

    private static string Issue(Guid merchantId, string[] roles, TimeSpan? lifetime)
    {
        var creds = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, merchantId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("merchantId", merchantId.ToString()),
            new("preferred_username", $"merchant-{merchantId:N}"),
        };
        foreach (var r in roles)
            claims.Add(new Claim("role", r));

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(15)),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
