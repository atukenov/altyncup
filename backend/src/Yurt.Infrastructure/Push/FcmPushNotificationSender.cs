using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Push;
using Yurt.Infrastructure.Firebase;

namespace Yurt.Infrastructure.Push;

/// <summary>FCM/APNs sender backed by the Firebase Admin SDK. The <see cref="FirebaseApp"/>
/// is shared through <see cref="FirebaseAppProvider"/> and built at most once per process. Disabled (<see cref="FirebaseOptions.Enabled"/> = false) or
/// missing credentials degrade to a no-op rather than throwing — this feature ships
/// before real Firebase credentials exist.</summary>
public class FcmPushNotificationSender : IPushNotificationSender
{
    private readonly FirebaseOptions _options;
    private readonly FirebaseAppProvider _apps;
    private readonly ILogger<FcmPushNotificationSender> _logger;

    public FcmPushNotificationSender(
        FirebaseOptions options, FirebaseAppProvider apps, ILogger<FcmPushNotificationSender> logger)
    {
        _options = options;
        _apps = apps;
        _logger = logger;
    }

    public async Task<PushSendResult> SendAsync(
        IReadOnlyList<string> tokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken ct = default)
    {
        if (tokens.Count == 0) return new PushSendResult([]);

        var app = GetOrCreateApp();
        if (app == null) return new PushSendResult([]);

        try
        {
            var message = new MulticastMessage
            {
                Tokens = tokens.ToList(),
                Notification = new Notification { Title = title, Body = body },
                Data = data,
            };

            var response = await FirebaseMessaging.GetMessaging(app).SendEachForMulticastAsync(message, ct);
            _logger.LogInformation(
                "FCM send: {SuccessCount} succeeded, {FailureCount} failed (of {Count}).",
                response.SuccessCount, response.FailureCount, tokens.Count);

            var dead = new List<string>();
            for (var i = 0; i < response.Responses.Count; i++)
            {
                var sendResponse = response.Responses[i];
                if (sendResponse.IsSuccess) continue;

                var errorCode = sendResponse.Exception?.MessagingErrorCode;
                if (errorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
                {
                    dead.Add(tokens[i]);
                }
                else
                {
                    _logger.LogWarning(sendResponse.Exception, "FCM send failed for one token: {ErrorCode}.", errorCode);
                }
            }
            return new PushSendResult(dead);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FCM send failed for {Count} token(s).", tokens.Count);
            return new PushSendResult([]);
        }
    }

    private FirebaseApp? GetOrCreateApp()
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Push notifications disabled — skipping FCM send.");
            return null;
        }
        return _apps.Get();
    }
}
