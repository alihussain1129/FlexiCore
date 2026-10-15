using FlexiCore.Tenancy.Data;
using FlexiCore.Tenancy.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlexiCore.Tenancy;

/// <summary>Entry points the host uses. Requires an NpgsqlDataSource to be registered by the host.</summary>
public static class TenancyModule
{
    public static IServiceCollection AddTenancyModule(this IServiceCollection services)
    {
        services.TryAddSingleton<ITenantConnectionFactory, TenantConnectionFactory>();
        services.TryAddSingleton<TenancyAuditWriter>();
        services.TryAddSingleton<TenancyStore>();
        return services;
    }

    public static IEndpointRouteBuilder MapTenancyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        TenancyEndpoints.Map(endpoints);
        return endpoints;
    }
}