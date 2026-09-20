using GreenFinance.Contracts.Events;
using MassTransit;
using ReportService.Domain;

namespace ReportService.Api.Consumers;

public sealed class EsgCalculatedEventConsumer(ITransactionRecordRepository repository) : IConsumer<EsgCalculatedEvent>
{
    public Task Consume(ConsumeContext<EsgCalculatedEvent> context)
    {
        var message = context.Message;
        return repository.ApplyEsgResultAsync(
            message.TransactionId, message.CompanyId, message.Category, message.Co2Kg, message.OverallScore, context.CancellationToken);
    }
}
