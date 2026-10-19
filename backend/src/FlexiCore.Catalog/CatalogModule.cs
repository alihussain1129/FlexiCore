using FlexiCore.Catalog.Data;
using FlexiCore.Catalog.Endpoints;
using FlexiCore.Tenancy;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlexiCore.Catalog;

/// <summary>Entry points the host uses. Requires an NpgsqlDataSource to be registered by the host.</summary>
public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.TryAddSingleton<ITenantConnectionFactory, TenantConnectionFactory>();
        services.TryAddSingleton<CatalogAuditWriter>();
        services.TryAddSingleton<CatalogStore>();
        return services;
    }

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        CatalogEndpoints.Map(endpoints);
        return endpoints;
    }
}