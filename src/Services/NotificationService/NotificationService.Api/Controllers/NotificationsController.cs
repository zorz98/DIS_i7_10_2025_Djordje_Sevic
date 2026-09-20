using Microsoft.AspNetCore.Mvc;
using NotificationService.Api.Contracts;
using NotificationService.Domain;

namespace NotificationService.Api.Controllers;

[ApiController]
[Route("notifications")]
public sealed class NotificationsController(INotificationRepository repository) : ControllerBase
{
    [HttpGet("company/{companyId:int}")]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetByCompany(int companyId, CancellationToken cancellationToken)
    {
        var notifications = await repository.GetByCompanyAsync(companyId, cancellationToken);
        return Ok(notifications.Select(NotificationDto.FromDomain));
    }
}
