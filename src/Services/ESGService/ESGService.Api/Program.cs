using System.Diagnostics.Metrics;
using ESGService.Api.Consumers;
using ESGService.Domain;
using ESGService.Infrastructure;
using GreenFinance.Observability;
using GreenFinance.ServiceDiscovery;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Polly;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddDbContext<EsgDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("EsgDb")));

builder.Services.AddScoped<IEsgResultRepository, EsgResultRepository>();
builder.Services.AddSingleton<Co2Calculator>();
builder.Services.AddSingleton<EsgScoreCalculator>();
builder.Services.AddScoped<EsgCalculationService>();
builder.Services.AddConsulServiceDiscovery(builder.Configuration);

builder.Services.AddSingleton(new Meter(EsgMetrics.MeterName));
builder.Services.AddSingleton<EsgMetrics>();
builder.Services.AddGreenFinanceMetrics("ESGService", EsgMetrics.MeterName);

builder.Services
    .AddHttpClient<IReferenceDataClient, ReferenceDataClient>(client =>
    {
        var baseUrl = builder.Configuration["ReferenceDataService:BaseUrl"] ?? "http://localhost:8080/";
        client.BaseAddress = new Uri(baseUrl);
        // No client.Timeout here: that raises a plain TaskCanceledException which
        // Polly's default transient-failure predicate does not recognize, so Retry
        // and CircuitBreaker would silently never engage. Polly's own AddTimeout
        // below raises TimeoutRejectedException, which IS handled by both.
    })
    .AddResilienceHandler("reference-data-pipeline", (pipeline, context) =>
    {
        var metrics = context.ServiceProvider.GetRequiredService<EsgMetrics>();

        // Retry a few times before giving the circuit breaker a chance to open.
        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromMilliseconds(200),
        });

        // Stop hammering ReferenceDataService once it is clearly down.
        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(10),
            MinimumThroughput = 3,
            BreakDuration = TimeSpan.FromSeconds(15),
            OnOpened = _ =>
            {
                metrics.SetCircuitOpen();
                return ValueTask.CompletedTask;
            },
            OnClosed = _ =>
            {
                metrics.SetCircuitClosed();
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = _ =>
            {
                metrics.SetCircuitHalfOpen();
                return ValueTask.CompletedTask;
            },
        });

        // Per-attempt timeout, innermost so each retry attempt gets its own budget.
        pipeline.AddTimeout(TimeSpan.FromSeconds(5));
    });

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<TransactionCreatedEventConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        // Read lazily: WebApplicationFactory-based tests inject configuration overrides
        // only after this configurator delegate is captured, but before it actually runs.
        var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
        var rabbitPort = builder.Configuration.GetValue<int?>("RabbitMq:Port") ?? 5672;

        cfg.Host(new Uri($"rabbitmq://{rabbitHost}:{rabbitPort}/"), h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });

        cfg.ReceiveEndpoint("esg-service-transaction-created", e =>
        {
            e.ConfigureConsumer<TransactionCreatedEventConsumer>(context);
        });
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<EsgDbContext>();
    MigrateDatabaseWithRetry(dbContext);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint();

app.Run();

static void MigrateDatabaseWithRetry(DbContext dbContext)
{
    const int maxAttempts = 5;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            dbContext.Database.Migrate();
            return;
        }
        catch (Exception) when (attempt < maxAttempts)
        {
            // When the target database doesn't exist yet, Database.Migrate() runs CREATE DATABASE /
            // ALTER DATABASE before EF Core's __EFMigrationsLock app-lock is available to serialize
            // it. Under concurrent replicas against a fresh volume, one replica's ALTER DATABASE can
            // block on another's in-progress CREATE DATABASE and hit the default 60s command timeout.
            // Back off and retry instead of letting that crash the container.
            Thread.Sleep(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
        }
    }
}

public partial class Program;
