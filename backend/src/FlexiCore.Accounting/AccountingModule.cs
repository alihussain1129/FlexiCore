using FlexiCore.Accounting.Data;
using FlexiCore.Accounting.Endpoints;
using FlexiCore.Tenancy;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlexiCore.Accounting;

/// <summary>Entry points the host uses. Requires an NpgsqlDataSource and the Tenancy module.</summary>
public static class AccountingModule
{
    public static IServiceCollection AddAccountingModule(this IServiceCollection services)
    {
        services.TryAddSingleton<ITenantConnectionFactory, TenantConnectionFactory>();
        services.TryAddSingleton<AccountingStore>();
        return services;
    }

    public static IEndpointRouteBuilder MapAccountingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        AccountingEndpoints.Map(endpoints);
        return endpoints;
    }
}
