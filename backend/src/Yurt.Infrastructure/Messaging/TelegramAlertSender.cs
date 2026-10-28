using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;

namespace Yurt.Infrastructure.Messaging;

/// <summary>
/// Posts admin alerts to a Telegram chat via the Bot API
/// (<c>https://api.telegram.org/bot{token}/sendMessage</c>). Registered as a singleton
/// so the per-key throttle survives across requests.
/// </summary>
public class TelegramAlertSender : IAdminAlertSender
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly AlertOptions _options;
    private readonly ILogger<TelegramAlertSender> _logger;
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastSent = new();

    public const string HttpClientName = "telegram-alerts";

    public TelegramAlertSender(
        IHttpClientFactory httpFactory, AlertOptions options, ILogger<TelegramAlertSender> logger,
        TimeProvider? clock = null)
    {
        _httpFactory = httpFactory;
        _options = options;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task SendAsync(string throttleKey, string text, CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("Admin alert not sent (Telegram not configured): {Alert}", text);
            return;
        }

        var now = _clock.GetUtcNow();
        var window = TimeSpan.FromMinutes(Math.Max(0, _options.ThrottleMinutes));
        var claimed = false;
        _lastSent.AddOrUpdate(throttleKey,
            _ => { claimed = true; return now; },
            (_, last) =>
            {
                if (now - last < window) { claimed = false; return last; }
                claimed = true;
                return now;
            });

        if (!claimed)
        {
            _logger.LogInformation("Admin alert throttled ({Key}): {Alert}", throttleKey, text);
            return;
        }

        await SendNowAsync(text, ct);
    }

    public async Task<bool> SendNowAsync(string text, CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("Admin alert not sent (Telegram not configured): {Alert}", text);
            return false;
        }

        try
        {
            var http = _httpFactory.CreateClient(HttpClientName);
            var url = $"https://api.telegram.org/bot{_options.TelegramBotToken.Trim()}/sendMessage";
            using var resp = await http.PostAsJsonAsync(url, new
            {
                chat_id = _options.TelegramChatId.Trim(),
                text = $"Altyncup: {text}",
                disable_web_page_preview = true,
            }, ct);

            if (resp.IsSuccessStatusCode)
                return true;

            var body = await resp.Content.ReadAsStringAsync(ct);
            _logger.LogError("Telegram alert failed with {Status}: {Body}. Alert was: {Alert}",
                (int)resp.StatusCode, GreenApi.Truncate(body, 500), text);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // The URL holds the bot token, so log only the exception type and message.
            _logger.LogError("Telegram alert could not be sent ({Error}: {Message}). Alert was: {Alert}",
                ex.GetType().Name, ex.Message, text);
            return false;
        }
    }
}
