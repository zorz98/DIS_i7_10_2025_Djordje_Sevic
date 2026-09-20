using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using Xunit;

namespace ReferenceDataService.IntegrationTests;

public sealed class ReferenceDataServiceApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder().Build();
    private readonly RedisContainer _redisContainer = new RedisBuilder().Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ReferenceDataDb"] = _sqlContainer.GetConnectionString(),
                ["Consul:Enabled"] = "false",
                ["Redis:ConnectionString"] = _redisContainer.GetConnectionString(),
            });
        });
    }

    public Task InitializeAsync() => Task.WhenAll(_sqlContainer.StartAsync(), _redisContainer.StartAsync());

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Task.WhenAll(_sqlContainer.DisposeAsync().AsTask(), _redisContainer.DisposeAsync().AsTask());
        await base.DisposeAsync();
    }
}
