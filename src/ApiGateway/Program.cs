using ApiGateway.ServiceDiscovery;
using GreenFinance.Observability;
using GreenFinance.ServiceDiscovery;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole();

var routes = new[]
{
    new RouteConfig
    {
        RouteId = "transactions-route",
        ClusterId = "transaction-service-cluster",
        Match = new RouteMatch { Path = "/transactions/{**catch-all}" },
    },
    new RouteConfig
    {
        RouteId = "esg-route",
        ClusterId = "esg-service-cluster",
        Match = new RouteMatch { Path = "/esg/{**catch-all}" },
    },
    new RouteConfig
    {
        RouteId = "categories-route",
        ClusterId = "reference-data-service-cluster",
        Match = new RouteMatch { Path = "/categories/{**catch-all}" },
    },
    new RouteConfig
    {
        RouteId = "emission-factors-route",
        ClusterId = "reference-data-service-cluster",
        Match = new RouteMatch { Path = "/emission-factors/{**catch-all}" },
    },
    new RouteConfig
    {
        RouteId = "reports-route",
        ClusterId = "report-service-cluster",
        Match = new RouteMatch { Path = "/reports/{**catch-all}" },
    },
    new RouteConfig
    {
        RouteId = "notifications-route",
        ClusterId = "notification-service-cluster",
        Match = new RouteMatch { Path = "/notifications/{**catch-all}" },
    },
};

// Cluster destinations start empty and are populated by ConsulProxyRefreshHostedService
// as soon as it runs its first Consul lookup (within a second or so of startup).
var configProvider = new ConsulProxyConfigProvider(routes, []);

builder.Services.AddSingleton(configProvider);
builder.Services.AddSingleton<IProxyConfigProvider>(configProvider);
builder.Services.AddReverseProxy();

builder.Services.AddConsulClient(builder.Configuration);
builder.Services.AddHostedService<ConsulProxyRefreshHostedService>();

builder.Services.AddGreenFinanceMetrics("ApiGateway");

var app = builder.Build();

app.MapReverseProxy();
app.MapPrometheusScrapingEndpoint();

app.Run();
