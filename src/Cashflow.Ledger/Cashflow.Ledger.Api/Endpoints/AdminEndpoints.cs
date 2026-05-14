using Cashflow.Ledger.Api.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Cashflow.Ledger.Api.Endpoints;

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/admin")
            .WithTags("admin")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        // §04 §3.5: `POST /admin/seed` será implementado em F4/F11 quando o Bogus stack chegar.
        // Este stub responde 501 para sinalizar a rota sem mascarar a falta de implementação.
        admin.MapPost("/seed", () => Results.StatusCode(StatusCodes.Status501NotImplemented))
            .WithName("AdminSeed");

        return app;
    }
}
