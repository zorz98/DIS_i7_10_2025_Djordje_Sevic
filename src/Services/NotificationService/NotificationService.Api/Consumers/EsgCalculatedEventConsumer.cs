using GreenFinance.Contracts.Events;
using MassTransit;
using NotificationService.Domain;

namespace NotificationService.Api.Consumers;

public sealed class EsgCalculatedEventConsumer(
    NotificationDecisionService decisionService,
    ILogger<EsgCalculatedEventConsumer> logger) : IConsumer<EsgCalculatedEvent>
{
    public async Task Consume(ConsumeContext<EsgCalculatedEvent> context)
    {
        var message = context.Message;

        var notification = await decisionService.ProcessAsync(
            message.TransactionId, message.CompanyId, message.OverallScore, context.CancellationToken);

        if (notification is not null)
        {
            logger.LogInformation(
                "Notification sent to company {CompanyId} for transaction {TransactionId}: {Message} (overallScore={OverallScore})",
                notification.CompanyId, notification.TransactionId, notification.Message, notification.OverallScore);
        }
    }
}
