using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Cashflow.TestSupport;

/// <summary>
/// Re-binds the JwtBearer handler on the WebApplicationFactory side to validate
/// tokens minted by <see cref="TestTokens"/>: same issuer, same symmetric key,
/// no remote discovery. The endpoints' Authorization policies stay untouched —
/// they continue to require <c>merchantId</c> + <c>role</c> claims, which the
/// test tokens already carry.
/// </summary>
public static class JwtTestAuthentication
{
    public static IServiceCollection ReplaceJwtForTests(this IServiceCollection services)
    {
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Authority = null;
            options.MetadataAddress = null!;
            options.RequireHttpsMetadata = false;
            options.Audience = TestTokens.Audience;
            options.ConfigurationManager = null;
            options.Configuration = null;
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = TestTokens.Issuer,
                ValidateAudience = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = TestTokens.SigningKey,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "preferred_username",
                RoleClaimType = "role"
            };
        });

        return services;
    }
}
