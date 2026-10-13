using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FlexiCore.IntegrationTests.Infrastructure;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    private readonly IDictionary<string, string?> _settings;
    private readonly Action<IServiceCollection>? _configureServices;

    public TestApiFactory(
        IDictionary<string, string?> settings,
        Action<IServiceCollection>? configureServices = null)
    {
        _settings = new Dictionary<string, string?>(settings);
        _configureServices = configureServices;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(_settings);
        });

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        if (_configureServices is not null)
        {
            builder.ConfigureTestServices(_configureServices);
        }
    }
}
