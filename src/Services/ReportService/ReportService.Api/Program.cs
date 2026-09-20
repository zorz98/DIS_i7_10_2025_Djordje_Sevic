using MassTransit;
using Microsoft.EntityFrameworkCore;
using ReportService.Api.Consumers;
using ReportService.Domain;
using ReportService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddDbContext<ReportDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ReportDb")));

builder.Services.AddScoped<ITransactionRecordRepository, TransactionRecordRepository>();
builder.Services.AddSingleton<ReportAggregator>();

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
