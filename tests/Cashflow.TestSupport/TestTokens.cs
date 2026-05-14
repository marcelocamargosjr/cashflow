using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Cashflow.TestSupport;

/// <summary>
/// Issues short-lived JWTs signed with a symmetric key so integration tests can
/// hit the JwtBearer-protected endpoints without spinning up Keycloak.
///
/// The factory side wires JwtBearer with <see cref="SigningKey"/> + <see cref="Issuer"/>
/// (via <c>JwtTestAuthentication</c>). Tokens follow the realm claim shape that the
/// production policies expect (`07 §3.1.2`):
///   - <c>merchantId</c>: the JWT claim carrying the merchant scope
///   - role/roles: the Keycloak realm-roles flattened (we emit `role` because
///     <c>JwtBearerOptions.TokenValidationParameters.RoleClaimType = "role"</c>)
/// </summary>
public static class TestTokens
{
    public const string Issuer = "https://cashflow-test/realm/cashflow";
    public const string Audience = "cashflow-api";

    /// <summary>HS256 signing key shared between the issuer and the JwtBearer side.</summary>
    public static readonly SymmetricSecurityKey SigningKey =
        new("cashflow-test-signing-key-do-not-use-in-prod-please-1234567890"u8.ToArray());

    /// <summary>
    /// Issues a token for a merchant with the <c>merchant</c> realm-role. The
    /// caller can pin <paramref name="merchantId"/> to drive resource-based
    /// authorization (e.g. <c>/balances/{merchantId}</c>).
    /// </summary>
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
