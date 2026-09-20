using Consul;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using YarpDestinationConfig = Yarp.ReverseProxy.Configuration.DestinationConfig;

namespace ApiGateway.ServiceDiscovery;

/// <summary>
/// Every <see cref="RefreshInterval"/>, resolves each known cluster's healthy
/// instances from Consul and pushes the result into <see cref="ConsulProxyConfigProvider"/>.
/// </summary>
public sealed class ConsulProxyRefreshHostedService(
    IConsulClient consulClient,
    ConsulProxyConfigProvider configProvider,
    ILogger<ConsulProxyRefreshHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(10);

    private static readonly (string ClusterId, string ConsulServiceName)[] Clusters =
    [
        ("transaction-service-cluster", "transaction-service"),
        ("esg-service-cluster", "esg-service"),
        ("reference-data-service-cluster", "reference-data-service"),
        ("report-service-cluster", "report-service"),
        ("notification-service-cluster", "notification-service"),
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);
        do
        {
            await RefreshAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var clusters = new List<ClusterConfig>(Clusters.Length);

        foreach (var (clusterId, serviceName) in Clusters)
        {
            try
            {
                var result = await consulClient.Health.Service(serviceName, tag: null, passingOnly: true, ct: cancellationToken);

                var destinations = result.Response
                    .Select((entry, index) => (entry, index))
                    .ToDictionary(
                        x => $"destination{x.index + 1}",
                        x => new YarpDestinationConfig { Address = $"http://{x.entry.Service.Address}:{x.entry.Service.Port}" });

                clusters.Add(new ClusterConfig { ClusterId = clusterId, Destinations = destinations });

                if (destinations.Count == 0)
                {
                    logger.LogWarning("No healthy Consul instances found for {ServiceName}", serviceName);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to resolve {ServiceName} from Consul; keeping cluster empty this cycle", serviceName);
                clusters.Add(new ClusterConfig { ClusterId = clusterId, Destinations = new Dictionary<string, YarpDestinationConfig>() });
            }
        }

        configProvider.Update(clusters);
    }
}
