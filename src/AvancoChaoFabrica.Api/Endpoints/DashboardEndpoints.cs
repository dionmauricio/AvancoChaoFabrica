using AvancoChaoFabrica.Api.Services;

namespace AvancoChaoFabrica.Api.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard", async (
            Guid? projectId,
            Guid? supplierId,
            Guid? productionOrderId,
            DashboardService dashboard,
            CancellationToken ct) =>
            Results.Ok(await dashboard.GetAsync(projectId, supplierId, productionOrderId, ct)))
            .WithTags("Dashboard");
        return app;
    }
}
