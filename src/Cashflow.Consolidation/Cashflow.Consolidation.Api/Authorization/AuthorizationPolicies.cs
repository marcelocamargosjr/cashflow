using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Cashflow.Consolidation.Api.Authorization;

internal static class AuthorizationPolicies
{
    public const string RequireMerchant = "RequireMerchant";
    public const string RequireAdmin = "RequireAdmin";

    public const string MerchantIdClaim = "merchantId";

    public const string AdminRole = "admin";

    public static IServiceCollection AddCashflowAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(RequireMerchant, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx =>
                    HasRealmRole(ctx.User, "merchant")
                    || HasRealmRole(ctx.User, AdminRole)));

            options.AddPolicy(RequireAdmin, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => HasRealmRole(ctx.User, AdminRole)));
        });

        return services;
    }

    public static Guid? GetMerchantId(this HttpContext httpContext)
    {
        var raw = httpContext.User.FindFirstValue(MerchantIdClaim);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static bool CanAccessMerchant(this HttpContext httpContext, Guid merchantId)
    {
        if (HasRealmRole(httpContext.User, AdminRole))
            return true;

        var claim = httpContext.GetMerchantId();
        return claim is not null && claim.Value == merchantId;
    }

    public static bool HasRealmRole(ClaimsPrincipal user, string role)
    {
        return user.IsInRole(role)
            || user.HasClaim(c => string.Equals(c.Type, "role", StringComparison.Ordinal) && string.Equals(c.Value, role, StringComparison.Ordinal))
            || user.HasClaim(c => string.Equals(c.Type, "roles", StringComparison.Ordinal) && string.Equals(c.Value, role, StringComparison.Ordinal));
    }
}
