using FirebaseAdmin.Auth;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Yurt.Application.Common.Interfaces;

namespace Yurt.Infrastructure.Firebase;

/// <summary>
/// Verifies Firebase ID tokens from phone sign-in with the Firebase Admin SDK (signature,
/// expiry, audience = our project) and reads the <c>phone_number</c> claim.
/// </summary>
public class FirebasePhoneVerifier : IFirebasePhoneVerifier
{
    private readonly FirebaseAppProvider _apps;
    private readonly ILogger<FirebasePhoneVerifier> _logger;

    public FirebasePhoneVerifier(FirebaseAppProvider apps, ILogger<FirebasePhoneVerifier> logger)
    {
        _apps = apps;
        _logger = logger;
    }

    public async Task<FirebasePhoneCheck> VerifyAsync(string idToken, CancellationToken ct = default)
    {
        var app = _apps.Get();
        if (app == null)
            return FirebasePhoneCheck.Invalid("Firebase is not configured on the server (Firebase__ServiceAccountJson).");

        FirebaseToken token;
        try
        {
            token = await FirebaseAuth.GetAuth(app).VerifyIdTokenAsync(idToken, ct);
        }
        catch (FirebaseAuthException ex)
        {
            _logger.LogWarning("Rejected Firebase ID token: {Code} {Message}", ex.AuthErrorCode, ex.Message);
            return FirebasePhoneCheck.Invalid($"Firebase token rejected: {ex.AuthErrorCode}");
        }
        catch (ArgumentException ex)
        {
            return FirebasePhoneCheck.Invalid($"Firebase token rejected: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Firebase ID token check failed.");
            return FirebasePhoneCheck.Invalid("Firebase token check failed: " + ex.Message);
        }

        return ReadPhone(token.Claims);
    }

    /// <summary>Pulls the phone number out of verified token claims; public for tests.</summary>
    public static FirebasePhoneCheck ReadPhone(IReadOnlyDictionary<string, object> claims)
    {
        var provider = claims.GetValueOrDefault("firebase") switch
        {
            JObject j => j.Value<string>("sign_in_provider"),
            IDictionary<string, object> d => d.TryGetValue("sign_in_provider", out var v) ? v as string : null,
            _ => null,
        };
        if (provider != null && provider != "phone")
            return FirebasePhoneCheck.Invalid($"Firebase token is from '{provider}' sign-in, not phone.");

        return claims.TryGetValue("phone_number", out var phone) && phone is string p && !string.IsNullOrWhiteSpace(p)
            ? FirebasePhoneCheck.Valid(p)
            : FirebasePhoneCheck.Invalid("Firebase token has no phone number.");
    }
}
