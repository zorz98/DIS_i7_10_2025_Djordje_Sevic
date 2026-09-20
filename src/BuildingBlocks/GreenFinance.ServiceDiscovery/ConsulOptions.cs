namespace GreenFinance.ServiceDiscovery;

public sealed class ConsulOptions
{
    public const string SectionName = "Consul";

    public bool Enabled { get; set; } = true;

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 8500;

    public required string ServiceName { get; set; }

    /// <summary>Defaults to <see cref="ServiceName"/> when not set explicitly.</summary>
    public string? ServiceAddress { get; set; }

    public int ServicePort { get; set; } = 8080;

    public string HealthCheckPath { get; set; } = "/health";
}
