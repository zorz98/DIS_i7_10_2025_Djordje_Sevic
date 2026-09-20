using System.Diagnostics.Metrics;
using GreenFinance.Observability;
using GreenFinance.ServiceDiscovery;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NotificationService.Api.Consumers;
using NotificationService.Domain;
using NotificationService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("NotificationDb")));

builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<NotificationDecisionService>();
builder.Services.AddConsulServiceDiscovery(builder.Configuration);

builder.Services.AddSingleton(new Meter(NotificationMetrics.MeterName));
builder.Services.AddSingleton<NotificationMetrics>();
builder.Services.AddGreenFinanceMetrics("NotificationService", NotificationMetrics.MeterName);

builder.Services.AddMassTransit(x =>
{
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

        cfg.ReceiveEndpoint("notification-service-esg-calculated", e =>
        {
            e.ConfigureConsumer<EsgCalculatedEventConsumer>(context);
        });
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
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
