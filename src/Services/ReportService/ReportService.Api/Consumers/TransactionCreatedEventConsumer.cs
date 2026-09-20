using GreenFinance.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Caching.Distributed;
using ReportService.Domain;

namespace ReportService.Api.Consumers;

public sealed class TransactionCreatedEventConsumer(ITransactionRecordRepository repository, IDistributedCache cache)
    : IConsumer<TransactionCreatedEvent>
{
    public async Task Consume(ConsumeContext<TransactionCreatedEvent> context)
    {
        var message = context.Message;
        var date = await repository.AddTransactionAsync(
            message.TransactionId, message.CompanyId, message.Category, message.Amount, message.Date, context.CancellationToken);
        await ReportCache.InvalidateAsync(cache, message.CompanyId, date.Year, date.Month, context.CancellationToken);
    }
}
