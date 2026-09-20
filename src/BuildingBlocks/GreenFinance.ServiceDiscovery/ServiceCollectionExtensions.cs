using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GreenFinance.ServiceDiscovery;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="IConsulClient"/> pointed at the "Consul" configuration
    /// section, for services that only need to query Consul (e.g. the gateway)
    /// without registering themselves.
    /// </summary>
    public static IServiceCollection AddConsulClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IConsulClient>(_ =>
        {
            var section = configuration.GetSection(ConsulOptions.SectionName);
            var host = section["Host"] ?? "localhost";
            var port = section.GetValue<int?>("Port") ?? 8500;
            return new ConsulClient(cfg => cfg.Address = new Uri($"http://{host}:{port}"));
        });

        return services;
    }

    /// <summary>
    /// Registers this service with Consul on startup (service discovery) using the
    /// "Consul" configuration section. Configuration is read lazily (inside the
    /// options binder / client factory, not into a variable up front) so
    /// WebApplicationFactory-based test overrides are still picked up correctly.
    /// </summary>
    public static IServiceCollection AddConsulServiceDiscovery(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ConsulOptions>(configuration.GetSection(ConsulOptions.SectionName));
        services.AddConsulClient(configuration);
        services.AddHostedService<ConsulRegistrationHostedService>();

        return services;
    }
}
