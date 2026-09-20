using Consul;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

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

        // Consul may still be starting up when this service does (e.g. a fresh
        // `docker compose up`) — retry a few times before giving up.
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await consulClient.Agent.ServiceRegister(registration, cancellationToken);
                logger.LogInformation("Registered {ServiceName} ({ServiceId}) with Consul", config.ServiceName, _registrationId);
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

    private static string ResolveAddress(ConsulOptions config)
    {
        if (!string.IsNullOrWhiteSpace(config.ServiceAddress))
        {
            return config.ServiceAddress;
        }

        try
        {
            // In Docker, a container's hostname is unique per container/replica and
            // resolvable by other containers on the same user-defined bridge network —
            // this is what lets multiple scaled replicas of the same service register
            // as distinct Consul catalog entries instead of colliding on ServiceName.
            return Dns.GetHostName();
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
