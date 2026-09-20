using GreenFinance.Contracts.Events;
using MassTransit;
using ReportService.Domain;

namespace ReportService.Api.Consumers;

public sealed class TransactionCreatedEventConsumer(ITransactionRecordRepository repository) : IConsumer<TransactionCreatedEvent>
{
    public Task Consume(ConsumeContext<TransactionCreatedEvent> context)
    {
        var message = context.Message;
        return repository.AddTransactionAsync(
            message.TransactionId, message.CompanyId, message.Category, message.Amount, message.Date, context.CancellationToken);
    }
}
