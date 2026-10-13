using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlexiCore.Tenancy;

/// <summary>Entry point the host uses. Requires an NpgsqlDataSource to be registered by the host.</summary>
public static class TenancyModule
{
    public static IServiceCollection AddTenancyModule(this IServiceCollection services)
    {
        services.TryAddSingleton<ITenantConnectionFactory, TenantConnectionFactory>();
        return services;
    }
}