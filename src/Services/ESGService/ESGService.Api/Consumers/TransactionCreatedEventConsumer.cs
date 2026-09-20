using ESGService.Domain;
using GreenFinance.Contracts.Events;
using MassTransit;

namespace ESGService.Api.Consumers;

public sealed class TransactionCreatedEventConsumer(
    EsgCalculationService calculationService,
    ILogger<TransactionCreatedEventConsumer> logger) : IConsumer<TransactionCreatedEvent>
{
    public async Task Consume(ConsumeContext<TransactionCreatedEvent> context)
    {
        var message = context.Message;

        var result = await calculationService.ProcessAsync(
            message.TransactionId, message.CompanyId, message.Category, message.Amount, context.CancellationToken);

        if (result.Status == EsgResultStatus.TemporarilyUnavailable)
        {
            logger.LogWarning(
                "ReferenceDataService temporarily unavailable, marked transaction {TransactionId} as pending",
                message.TransactionId);
            return;
        }

        await context.Publish(new EsgCalculatedEvent(
            result.TransactionId,
            result.CompanyId,
            result.Category,
            result.Co2Kg,
            result.EnvironmentalScore,
            result.OverallScore,
            result.CalculatedAt));
    }
}
