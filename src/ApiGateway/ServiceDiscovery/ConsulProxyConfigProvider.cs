using Yarp.ReverseProxy.Configuration;

namespace ApiGateway.ServiceDiscovery;

/// <summary>
/// Supplies YARP's cluster destinations dynamically from Consul's service catalog
/// instead of a static appsettings.json section. Routes are fixed (defined once at
/// startup); only cluster destinations are refreshed, by
/// <see cref="ConsulProxyRefreshHostedService"/>.
/// </summary>
public sealed class ConsulProxyConfigProvider : IProxyConfigProvider
{
    private readonly IReadOnlyList<RouteConfig> _routes;
    private volatile ConsulProxyConfig _config;

    public ConsulProxyConfigProvider(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> initialClusters)
    {
        _routes = routes;
        _config = new ConsulProxyConfig(routes, initialClusters);
    }

    public IProxyConfig GetConfig() => _config;

    public void Update(IReadOnlyList<ClusterConfig> clusters)
    {
        var oldConfig = _config;
        _config = new ConsulProxyConfig(_routes, clusters);
        oldConfig.SignalChange();
    }
}
