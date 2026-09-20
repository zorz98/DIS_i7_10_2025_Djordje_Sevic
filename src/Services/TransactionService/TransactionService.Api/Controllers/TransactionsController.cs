using GreenFinance.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using TransactionService.Api.Contracts;
using TransactionService.Domain;

namespace TransactionService.Api.Controllers;

[ApiController]
[Route("transactions")]
public sealed class TransactionsController(
    ITransactionRepository repository,
    IPublishEndpoint publishEndpoint) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TransactionDto>> Create(CreateTransactionRequest request, CancellationToken cancellationToken)
    {
        var transaction = Transaction.Create(request.CompanyId, request.Category, request.Amount, request.Currency, request.Date);
        await repository.AddAsync(transaction, cancellationToken);

        await publishEndpoint.Publish(new TransactionCreatedEvent(
            transaction.Id,
            transaction.CompanyId,
            transaction.Category,
            transaction.Amount,
            transaction.Currency,
            transaction.Date), cancellationToken);

        var dto = TransactionDto.FromDomain(transaction);
        return CreatedAtAction(nameof(GetById), new { id = transaction.Id }, dto);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TransactionDto>>> GetAll(CancellationToken cancellationToken)
    {
        var transactions = await repository.GetAllAsync(cancellationToken);
        return Ok(transactions.Select(TransactionDto.FromDomain));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await repository.GetByIdAsync(id, cancellationToken);
        return transaction is null ? NotFound() : Ok(TransactionDto.FromDomain(transaction));
    }

    [HttpGet("company/{companyId:int}")]
    public async Task<ActionResult<IReadOnlyList<TransactionDto>>> GetByCompany(int companyId, CancellationToken cancellationToken)
    {
        var transactions = await repository.GetByCompanyAsync(companyId, cancellationToken);
        return Ok(transactions.Select(TransactionDto.FromDomain));
    }
}
