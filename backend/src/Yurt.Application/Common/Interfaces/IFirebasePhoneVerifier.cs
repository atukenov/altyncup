namespace Yurt.Application.Common.Interfaces;

/// <summary>
/// Checks a Firebase ID token the customer app got from Firebase Phone Auth after the
/// customer typed the SMS code.
/// </summary>
public interface IFirebasePhoneVerifier
{
    /// <summary>
    /// Returns the verified phone number (E.164) from the token, or an error explaining why
    /// the token was rejected. Never throws for invalid tokens or Firebase errors.
    /// </summary>
    Task<FirebasePhoneCheck> VerifyAsync(string idToken, CancellationToken ct = default);
}

public record FirebasePhoneCheck(string? PhoneNumber, string? Error)
{
    public bool Ok => Error == null && PhoneNumber != null;

    public static FirebasePhoneCheck Valid(string phoneNumber) => new(phoneNumber, null);
    public static FirebasePhoneCheck Invalid(string error) => new(null, error);
}
