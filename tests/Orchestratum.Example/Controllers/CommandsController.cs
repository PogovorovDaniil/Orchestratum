using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orchestratum.Database;
using Orchestratum.Example.Models;

namespace Orchestratum.Example.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommandsController : ControllerBase
{
    private readonly DbContextOptions<OrchDbContext> _contextOptions;

    public CommandsController(DbContextOptions<OrchDbContext> contextOptions)
    {
        _contextOptions = contextOptions;
    }

    [HttpGet]
    public async Task<IActionResult> GetCommands([FromQuery] int limit = 50)
    {
        using var context = new OrchDbContext(_contextOptions, "ORCH_");

        var commands = await context.Commands
            .OrderByDescending(c => c.ScheduledAt)
            .Take(limit)
            .Select(c => new CommandInfo
            {
                Id = c.Id,
                Name = c.Name,
                Target = c.Target,
                ScheduledAt = c.ScheduledAt.DateTime,
                RunningAt = c.RunningAt.HasValue ? c.RunningAt.Value.DateTime : null,
                CompletedAt = c.CompletedAt.HasValue ? c.CompletedAt.Value.DateTime : null,
                IsRunning = c.IsRunning,
                IsCompleted = c.IsCompleted,
                IsFailed = c.IsFailed,
                IsCanceled = c.IsCanceled,
                RetriesLeft = c.RetriesLeft,
                Timeout = c.Timeout
            })
            .ToListAsync();

        return Ok(commands);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        using var context = new OrchDbContext(_contextOptions, "ORCH_");

        var total = await context.Commands.CountAsync();
        var completed = await context.Commands.CountAsync(c => c.IsCompleted);
        var failed = await context.Commands.CountAsync(c => c.IsFailed);
        var running = await context.Commands.CountAsync(c => c.IsRunning);
        var pending = await context.Commands.CountAsync(c =>
            !c.IsRunning && !c.IsCompleted && !c.IsFailed && !c.IsCanceled);

        return Ok(new
        {
            total,
            completed,
            failed,
            running,
            pending
        });
    }
}
