using GreenFinance.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Caching.Distributed;
using ReportService.Domain;

namespace ReportService.Api.Consumers;

public sealed class EsgCalculatedEventConsumer(ITransactionRecordRepository repository, IDistributedCache cache)
    : IConsumer<EsgCalculatedEvent>
{
    public async Task Consume(ConsumeContext<EsgCalculatedEvent> context)
    {
        var message = context.Message;
        var date = await repository.ApplyEsgResultAsync(
            message.TransactionId, message.CompanyId, message.Category, message.Co2Kg, message.OverallScore, context.CancellationToken);
        await ReportCache.InvalidateAsync(cache, message.CompanyId, date.Year, date.Month, context.CancellationToken);
    }
}
