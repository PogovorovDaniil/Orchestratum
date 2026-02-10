using Microsoft.AspNetCore.Mvc;
using Orchestratum.Example.Services;

namespace Orchestratum.Example.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly NotificationService _notificationService;

    public NotificationsController(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public IActionResult GetNotifications()
    {
        var logs = _notificationService.GetLogs();
        return Ok(logs);
    }
}
