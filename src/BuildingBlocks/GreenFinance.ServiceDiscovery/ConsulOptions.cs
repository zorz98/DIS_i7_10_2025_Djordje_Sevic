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
}
