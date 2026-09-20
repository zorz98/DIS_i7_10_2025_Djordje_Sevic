using Consul;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;

namespace GreenFinance.ServiceDiscovery;

/// <summary>
/// Registers this service instance with Consul on startup and deregisters it on
/// shutdown. Registration failure is never fatal — a service should not refuse to
/// start just because Consul is temporarily unreachable (this also makes it a safe
/// no-op in test environments that don't run Consul at all).
/// </summary>
public sealed class ConsulRegistrationHostedService(
    IConsulClient consulClient,
    IOptions<ConsulOptions> options,
    ILogger<ConsulRegistrationHostedService> logger) : IHostedService
{
    private const int MaxAttempts = 3;

    private string? _registrationId;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var config = options.Value;
        if (!config.Enabled)
        {
            return;
        }

        var address = ResolveAddress(config);
        _registrationId = $"{config.ServiceName}-{address}-{config.ServicePort}";

        var registration = new AgentServiceRegistration
        {
            ID = _registrationId,
            Name = config.ServiceName,
            Address = address,
            Port = config.ServicePort,
            Check = new AgentServiceCheck
            {
                HTTP = $"http://{address}:{config.ServicePort}{config.HealthCheckPath}",
                Interval = TimeSpan.FromSeconds(10),
                Timeout = TimeSpan.FromSeconds(5),
                DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1),
            },
        };

        if (config.Sidecar is { Enabled: true } sidecar)
        {
            // Consul rejects an explicit ID on SidecarService ("managed by the agent") — it
            // always auto-generates "<parent-id>-sidecar-proxy". Since the parent ID here is
            // itself hostname-derived (see ResolveAddress), the sidecar container can't know
            // this value ahead of time from a static compose command-line flag. Instead, once
            // registration succeeds below, we write the resolved ID to a file on a volume
            // shared with the sidecar container, which reads it via consul-dataplane's
            // `-proxy-id-path` flag.
            registration.Connect = new AgentServiceConnect
            {
                SidecarService = new AgentServiceRegistration
                {
                    Proxy = sidecar.Upstreams.Count == 0
                        ? null
                        : new AgentServiceProxy
                        {
                            Upstreams = sidecar.Upstreams
                                .Select(u => new AgentServiceProxyUpstream { DestinationName = u.DestinationName, LocalBindPort = u.LocalBindPort })
                                .ToArray(),
                        },
                },
            };
        }

        // Consul may still be starting up when this service does (e.g. a fresh
        // `docker compose up`) — retry a few times before giving up.
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await consulClient.Agent.ServiceRegister(registration, cancellationToken);
                logger.LogInformation("Registered {ServiceName} ({ServiceId}) with Consul", config.ServiceName, _registrationId);

                if (config.Sidecar is { Enabled: true } enabledSidecar)
                {
                    WriteSidecarProxyIdFile(enabledSidecar, _registrationId);
                }

                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                logger.LogWarning(ex, "Consul registration attempt {Attempt}/{MaxAttempts} failed, retrying", attempt, MaxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Could not register {ServiceName} with Consul after {MaxAttempts} attempts; continuing without service discovery registration",
                    config.ServiceName,
                    MaxAttempts);
            }
        }
    }

    private void WriteSidecarProxyIdFile(ConsulSidecarOptions sidecar, string parentRegistrationId)
    {
        try
        {
            var directory = Path.GetDirectoryName(sidecar.ProxyIdFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(sidecar.ProxyIdFilePath, $"{parentRegistrationId}-sidecar-proxy");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write sidecar proxy ID file to {Path}", sidecar.ProxyIdFilePath);
        }
    }

    private static string ResolveAddress(ConsulOptions config)
    {
        if (!string.IsNullOrWhiteSpace(config.ServiceAddress))
        {
            return config.ServiceAddress;
        }

        try
        {
            // Resolve to this container's actual IP (not just its hostname string) —
            // unique per container/replica like the hostname would be, but also a valid
            // literal address for Consul Connect sidecars, which bind their Envoy
            // listener directly to this value and reject a non-IP hostname.
            var hostEntry = Dns.GetHostEntry(Dns.GetHostName());
            var ipv4 = hostEntry.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
            return ipv4?.ToString() ?? Dns.GetHostName();
        }
        catch (Exception)
        {
            return config.ServiceName;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_registrationId is null)
        {
            return;
        }

        try
        {
            await consulClient.Agent.ServiceDeregister(_registrationId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deregister {ServiceId} from Consul on shutdown", _registrationId);
        }
    }
}
