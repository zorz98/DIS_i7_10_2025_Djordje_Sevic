using System.Diagnostics.Metrics;
using FluentAssertions;
using Moq;
using NotificationService.Domain;
using Xunit;

namespace NotificationService.UnitTests;

public class NotificationDecisionServiceTests
{
    private readonly Mock<INotificationRepository> _repository = new();
    private readonly NotificationMetrics _metrics = new(new Meter("NotificationService.UnitTests"));

    private NotificationDecisionService CreateService() => new(_repository.Object, _metrics);

    [Fact]
    public async Task ProcessAsync_Should_Create_Notification_When_Score_Below_Threshold()
    {
        var service = CreateService();
        var transactionId = Guid.NewGuid();

        var notification = await service.ProcessAsync(transactionId, 55, overallScore: 25, CancellationToken.None);

        notification.Should().NotBeNull();
        notification!.Message.Should().Be("Your ESG score requires attention");

        _repository.Verify(r => r.AddAsync(
            It.Is<Notification>(n => n.TransactionId == transactionId && n.OverallScore == 25),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_Should_Not_Create_Notification_When_Score_At_Or_Above_Threshold()
    {
        var service = CreateService();

        var notification = await service.ProcessAsync(Guid.NewGuid(), 55, overallScore: 40, CancellationToken.None);

        notification.Should().BeNull();
        _repository.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(39, true)]
    [InlineData(40, false)]
    [InlineData(100, false)]
    public void ShouldNotify_Should_Match_LowScoreThreshold(int overallScore, bool expected)
    {
        Notification.ShouldNotify(overallScore).Should().Be(expected);
    }
}
