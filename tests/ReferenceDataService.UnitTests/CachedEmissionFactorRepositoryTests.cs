using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReferenceDataService.Domain;
using ReferenceDataService.Infrastructure;
using StackExchange.Redis;
using Xunit;

namespace ReferenceDataService.UnitTests;

public class CachedEmissionFactorRepositoryTests
{
    private readonly Mock<IEmissionFactorRepository> _inner = new();
    private readonly Mock<IDistributedCache> _cache = new();

    private CachedEmissionFactorRepository CreateSut() =>
        new(_inner.Object, _cache.Object, NullLogger<CachedEmissionFactorRepository>.Instance);

    [Fact]
    public async Task GetByCategoryAsync_Should_Call_Inner_And_Populate_Cache_On_Miss()
    {
        _cache.Setup(c => c.GetAsync("emission-factor:Fuel", It.IsAny<CancellationToken>())).ReturnsAsync((byte[]?)null);
        _inner
            .Setup(r => r.GetByCategoryAsync("Fuel", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmissionFactor { Id = 1, Category = "Fuel", Co2FactorPerEur = 2.31m });

        var sut = CreateSut();

        var result = await sut.GetByCategoryAsync("Fuel", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Category.Should().Be("Fuel");
        _inner.Verify(r => r.GetByCategoryAsync("Fuel", It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(
            c => c.SetAsync("emission-factor:Fuel", It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByCategoryAsync_Should_Not_Call_Inner_On_Cache_Hit()
    {
        var cached = """{"Id":1,"Category":"Fuel","Co2FactorPerEur":2.31}"""u8.ToArray();
        _cache.Setup(c => c.GetAsync("emission-factor:Fuel", It.IsAny<CancellationToken>())).ReturnsAsync(cached);

        var sut = CreateSut();

        var result = await sut.GetByCategoryAsync("Fuel", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Category.Should().Be("Fuel");
        result.Co2FactorPerEur.Should().Be(2.31m);
        _inner.Verify(r => r.GetByCategoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByCategoryAsync_Should_Fall_Back_To_Inner_When_Redis_Throws()
    {
        _cache
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "boom"));
        _cache
            .Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "boom"));
        _inner
            .Setup(r => r.GetByCategoryAsync("Fuel", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmissionFactor { Id = 1, Category = "Fuel", Co2FactorPerEur = 2.31m });

        var sut = CreateSut();

        var result = await sut.GetByCategoryAsync("Fuel", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Category.Should().Be("Fuel");
        _inner.Verify(r => r.GetByCategoryAsync("Fuel", It.IsAny<CancellationToken>()), Times.Once);
    }
}
