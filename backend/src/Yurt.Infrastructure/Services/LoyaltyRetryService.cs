using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yurt.Application.Features.Loyalty;
using Yurt.Application.Features.Loyalty.Services;

namespace Yurt.Infrastructure.Services;

/// <summary>
/// Retries iiko wallet operations (chargeoff / hold release) that failed while iiko
/// was unavailable, so no hold is ever stranded and no spend goes uncharged.
/// </summary>
public class LoyaltyRetryService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceProvider _services;
    private readonly IikoOptions _options;
    private readonly ILogger<LoyaltyRetryService> _logger;

    public LoyaltyRetryService(
        IServiceProvider services, IikoOptions options, ILogger<LoyaltyRetryService> logger)
    {
        _services = services;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _services.CreateAsyncScope();
                var loyalty = scope.ServiceProvider.GetRequiredService<LoyaltyService>();
                await loyalty.RetryPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loyalty retry sweep failed");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
