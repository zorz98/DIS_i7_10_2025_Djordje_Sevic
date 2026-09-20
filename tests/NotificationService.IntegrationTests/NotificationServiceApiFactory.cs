using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace NotificationService.IntegrationTests;

public sealed class NotificationServiceApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder().Build();

    private readonly RabbitMqContainer _rabbitContainer = new RabbitMqBuilder()
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:NotificationDb"] = _sqlContainer.GetConnectionString(),
                ["RabbitMq:Host"] = _rabbitContainer.Hostname,
                ["RabbitMq:Port"] = _rabbitContainer.GetMappedPublicPort(5672).ToString(),
                ["RabbitMq:Username"] = "guest",
                ["RabbitMq:Password"] = "guest",
                ["Consul:Enabled"] = "false",
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
        await _rabbitContainer.StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
        await _rabbitContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}
