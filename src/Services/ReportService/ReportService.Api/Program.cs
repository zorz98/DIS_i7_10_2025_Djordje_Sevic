using GreenFinance.Observability;
using GreenFinance.ServiceDiscovery;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using ReportService.Api.Consumers;
using ReportService.Domain;
using ReportService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddDbContext<ReportDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ReportDb")));

builder.Services.AddStackExchangeRedisCache(options =>
{
    var connectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
    options.ConfigurationOptions = new StackExchange.Redis.ConfigurationOptions
    {
        EndPoints = { connectionString },
        AbortOnConnectFail = false,
        ConnectTimeout = 500,
        SyncTimeout = 500,
        AsyncTimeout = 500,
        ConnectRetry = 1,
        ReconnectRetryPolicy = new StackExchange.Redis.LinearRetry(500),
    };
});

builder.Services.AddScoped<ITransactionRecordRepository, TransactionRecordRepository>();
builder.Services.AddSingleton<ReportAggregator>();
builder.Services.AddConsulServiceDiscovery(builder.Configuration);
builder.Services.AddGreenFinanceMetrics("ReportService");

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<TransactionCreatedEventConsumer>();
    x.AddConsumer<EsgCalculatedEventConsumer>();

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

        cfg.ReceiveEndpoint("report-service-transaction-created", e =>
        {
            e.ConfigureConsumer<TransactionCreatedEventConsumer>(context);
        });

        cfg.ReceiveEndpoint("report-service-esg-calculated", e =>
        {
            e.ConfigureConsumer<EsgCalculatedEventConsumer>(context);
        });
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
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
