using MassTransit;
using Microsoft.EntityFrameworkCore;
using NotificationService.Api.Consumers;
using NotificationService.Domain;
using NotificationService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("NotificationDb")));

builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<NotificationDecisionService>();

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
