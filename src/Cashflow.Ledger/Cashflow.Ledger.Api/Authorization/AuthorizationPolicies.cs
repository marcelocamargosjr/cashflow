using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Cashflow.Ledger.Api.Authorization;

internal static class AuthorizationPolicies
{
    public const string RequireMerchant = "RequireMerchant";
    public const string RequireAdmin = "RequireAdmin";

    // Custom Keycloak claim, mapped via the realm protocol-mapper documented em §07 §3.1.2.
    public const string MerchantIdClaim = "merchantId";

    public static void AddCashflowAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(RequireMerchant, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx =>
                    HasRealmRole(ctx.User, "merchant")
                    || HasRealmRole(ctx.User, "admin")));

            options.AddPolicy(RequireAdmin, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => HasRealmRole(ctx.User, "admin")));
        });
    }

    public static Guid? GetMerchantId(this HttpContext httpContext)
    {
        var raw = httpContext.User.FindFirstValue(MerchantIdClaim);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private static bool HasRealmRole(ClaimsPrincipal user, string role)
    {
        // Default Microsoft mapping puts Keycloak realm roles into ClaimTypes.Role too.
        return user.IsInRole(role)
            || user.HasClaim(c => c.Type == "role" && string.Equals(c.Value, role, StringComparison.Ordinal))
            || user.HasClaim(c => c.Type == "roles" && string.Equals(c.Value, role, StringComparison.Ordinal));
    }
}
