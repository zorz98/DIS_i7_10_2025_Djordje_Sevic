using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace GreenFinance.Observability;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Wires up OpenTelemetry metrics (ASP.NET Core, HttpClient, .NET runtime,
    /// MassTransit, and any service-specific meters) with a Prometheus exporter.
    /// Pair with <c>app.MapPrometheusScrapingEndpoint()</c> to expose "/metrics".
    /// </summary>
    public static IServiceCollection AddGreenFinanceMetrics(
        this IServiceCollection services, string serviceName, params string[] additionalMeterNames)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("MassTransit")
                    .AddPrometheusExporter();

                foreach (var meterName in additionalMeterNames)
                {
                    metrics.AddMeter(meterName);
                }
            });

        return services;
    }
}
