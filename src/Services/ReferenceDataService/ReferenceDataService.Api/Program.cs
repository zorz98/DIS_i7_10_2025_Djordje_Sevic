using GreenFinance.Observability;
using GreenFinance.ServiceDiscovery;
using Microsoft.EntityFrameworkCore;
using ReferenceDataService.Domain;
using ReferenceDataService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddDbContext<ReferenceDataDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ReferenceDataDb")));

builder.Services.AddStackExchangeRedisCache(options =>
{
    var connectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
    options.ConfigurationOptions = new StackExchange.Redis.ConfigurationOptions
    {
        EndPoints = { connectionString },
        // Redis is a best-effort cache in front of the database (see
        // CachedEmissionFactorRepository) — if it's unreachable, requests should
        // fail fast into the cache-miss fallback path instead of hanging.
        AbortOnConnectFail = false,
        ConnectTimeout = 500,
        SyncTimeout = 500,
        AsyncTimeout = 500,
        ConnectRetry = 1,
        ReconnectRetryPolicy = new StackExchange.Redis.LinearRetry(500),
    };
});

builder.Services.AddScoped<EmissionFactorRepository>();
builder.Services.AddScoped<IEmissionFactorRepository>(sp => new CachedEmissionFactorRepository(
    sp.GetRequiredService<EmissionFactorRepository>(),
    sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>(),
    sp.GetRequiredService<ILogger<CachedEmissionFactorRepository>>()));
builder.Services.AddConsulServiceDiscovery(builder.Configuration);
builder.Services.AddGreenFinanceMetrics("ReferenceDataService");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ReferenceDataDbContext>();
    dbContext.Database.Migrate();
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

public partial class Program;
