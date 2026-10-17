using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yurt.Application.Features.Engagement;

namespace Yurt.Infrastructure.Services;

/// <summary>Hourly tick for <see cref="EngagementService"/>. Does nothing while both pushes are off.</summary>
public class EngagementPushService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EngagementOptions _options;
    private readonly ILogger<EngagementPushService> _logger;

    public EngagementPushService(
        IServiceScopeFactory scopeFactory, EngagementOptions options, ILogger<EngagementPushService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.BirthdayPushEnabled && !_options.WinBackPushEnabled) return;

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<EngagementService>();
                await svc.RunAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Engagement push run failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
