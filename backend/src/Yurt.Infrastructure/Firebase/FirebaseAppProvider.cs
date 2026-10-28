using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Yurt.Application.Features.Push;

namespace Yurt.Infrastructure.Firebase;

/// <summary>
/// Builds the process-wide <see cref="FirebaseApp"/> once, from
/// <c>Firebase__ServiceAccountJson</c>, for push notifications and phone-auth token checks.
/// Returns null (and logs once) when the service account is missing or invalid.
/// </summary>
public class FirebaseAppProvider
{
    private readonly FirebaseOptions _options;
    private readonly ILogger<FirebaseAppProvider> _logger;
    private readonly object _initLock = new();
    private FirebaseApp? _app;
    private bool _initFailed;

    public FirebaseAppProvider(FirebaseOptions options, ILogger<FirebaseAppProvider> logger)
    {
        _options = options;
        _logger = logger;
    }

    public FirebaseApp? Get()
    {
        if (_app != null) return _app;
        if (_initFailed) return null;

        lock (_initLock)
        {
            if (_app != null) return _app;
            if (_initFailed) return null;

            if (string.IsNullOrWhiteSpace(_options.ServiceAccountJson))
            {
                _logger.LogWarning("Firebase__ServiceAccountJson is not set — Firebase is unavailable.");
                _initFailed = true;
                return null;
            }

            try
            {
                _app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromJson(_options.ServiceAccountJson),
                    ProjectId = string.IsNullOrWhiteSpace(_options.ProjectId) ? null : _options.ProjectId,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize FirebaseApp.");
                _initFailed = true;
            }
            return _app;
        }
    }
}
