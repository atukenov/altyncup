using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// A pending phone-number verification for customer registration.
/// Holds the one-time code (hashed) plus the registration payload the customer
/// submitted, so the CustomerUser is only created once the code is confirmed.
/// One row per mobile number — a new request overwrites the previous one.
/// </summary>
public class PhoneVerification : BaseEntity
{
    public string MobileNumber { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime LastSentAt { get; set; }
    public int Attempts { get; set; }

    // Pending registration payload — applied when the code is confirmed.
    public string PinHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
