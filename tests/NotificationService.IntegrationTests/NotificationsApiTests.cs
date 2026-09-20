using System.Net.Http.Json;
using FluentAssertions;
using GreenFinance.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace NotificationService.IntegrationTests;

public class NotificationsApiTests(NotificationServiceApiFactory factory) : IClassFixture<NotificationServiceApiFactory>
{
    private sealed record NotificationDto(Guid Id, Guid TransactionId, int CompanyId, string Message, int OverallScore, DateTimeOffset SentAt);

    [Fact]
    public async Task Consumer_Should_Create_Notification_When_EsgCalculatedEvent_Has_Low_Score()
    {
        var companyId = 7777;
        var transactionId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
            await publishEndpoint.Publish(new EsgCalculatedEvent(
                transactionId, companyId, "Fuel", 11550m, 54, 25, DateTimeOffset.UtcNow));
        }

        var client = factory.CreateClient();
        List<NotificationDto>? notifications = null;

        for (var attempt = 0; attempt < 20 && (notifications is null || notifications.Count == 0); attempt++)
        {
            await Task.Delay(500);
            var response = await client.GetAsync($"/notifications/company/{companyId}");
            response.EnsureSuccessStatusCode();
            notifications = await response.Content.ReadFromJsonAsync<List<NotificationDto>>();
        }

        notifications.Should().NotBeNull();
        notifications!.Should().ContainSingle(n => n.TransactionId == transactionId && n.OverallScore == 25);
    }

    [Fact]
    public async Task GetByCompany_Should_Return_Empty_List_For_Company_With_No_Notifications()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/notifications/company/999999");
        response.EnsureSuccessStatusCode();

        var notifications = await response.Content.ReadFromJsonAsync<List<NotificationDto>>();

        notifications.Should().NotBeNull().And.BeEmpty();
    }
}
