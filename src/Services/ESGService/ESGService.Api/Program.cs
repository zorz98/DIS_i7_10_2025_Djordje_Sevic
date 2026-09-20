using ESGService.Api.Consumers;
using ESGService.Domain;
using ESGService.Infrastructure;
using GreenFinance.ServiceDiscovery;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Polly;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services
    .AddHttpClient<IReferenceDataClient, ReferenceDataClient>(client =>
    {
        var baseUrl = builder.Configuration["ReferenceDataService:BaseUrl"] ?? "http://localhost:8080/";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(5);
    })
    .AddResilienceHandler("reference-data-pipeline", pipeline =>
    {
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
        });
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
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
