using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GreenFinance.ServiceDiscovery;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers this service with Consul on startup (service discovery) using the
    /// "Consul" configuration section. Configuration is read lazily (inside the
    /// options binder / client factory, not into a variable up front) so
    /// WebApplicationFactory-based test overrides are still picked up correctly.
    /// </summary>
    public static IServiceCollection AddConsulServiceDiscovery(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ConsulOptions>(configuration.GetSection(ConsulOptions.SectionName));

        services.AddSingleton<IConsulClient>(_ =>
        {
            var section = configuration.GetSection(ConsulOptions.SectionName);
            var host = section["Host"] ?? "localhost";
            var port = section.GetValue<int?>("Port") ?? 8500;
            return new ConsulClient(cfg => cfg.Address = new Uri($"http://{host}:{port}"));
        });

        services.AddHostedService<ConsulRegistrationHostedService>();

        return services;
    }
}
