namespace GreenFinance.ServiceDiscovery;

public sealed class ConsulOptions
{
    public const string SectionName = "Consul";

    public bool Enabled { get; set; } = true;

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 8500;

    public required string ServiceName { get; set; }

    /// <summary>
    /// Defaults to the container/machine hostname (<see cref="System.Net.Dns.GetHostName"/>)
    /// when not set explicitly, so each replica of a scaled service registers with Consul
    /// under a distinct address instead of every replica colliding on the same value. Set
    /// explicitly to pin a fixed address (e.g. for environments where the hostname isn't
    /// network-resolvable by other containers).
    /// </summary>
    public string? ServiceAddress { get; set; }

    public int ServicePort { get; set; } = 8080;

    public string HealthCheckPath { get; set; } = "/health";

    /// <summary>
    /// Optional Consul Connect sidecar registration. Left <c>null</c> (the
    /// default) for every service that doesn't participate in the scoped
    /// service mesh — this is purely additive and does not change registration
    /// behavior for services that don't configure it.
    /// </summary>
    public ConsulSidecarOptions? Sidecar { get; set; }
}

public sealed class ConsulSidecarOptions
{
    public bool Enabled { get; set; }

    public List<ConsulUpstreamOptions> Upstreams { get; set; } = [];

    /// <summary>
    /// Path (on a volume shared with the sidecar container) where the resolved
    /// sidecar proxy service ID is written after registration, for the sidecar
    /// container to read via consul-dataplane's <c>-proxy-id-path</c> flag.
    /// </summary>
    public string ProxyIdFilePath { get; set; } = "/consul-sidecar/proxy-id";
}

public sealed class ConsulUpstreamOptions
{
    public required string DestinationName { get; set; }

    public int LocalBindPort { get; set; }
}
