using GreenFinance.Observability;
using GreenFinance.ServiceDiscovery;
using Microsoft.EntityFrameworkCore;
using ReferenceDataService.Domain;
using ReferenceDataService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddDbContext<ReferenceDataDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ReferenceDataDb")));

builder.Services.AddScoped<IEmissionFactorRepository, EmissionFactorRepository>();
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
