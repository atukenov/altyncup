using Yurt.Domain.Common;
using Yurt.Domain.Enums;

namespace Yurt.Domain.Entities;

/// <summary>
/// Append-only history of one-time-code activity. Unlike <see cref="PhoneVerification"/>
/// (deleted once confirmed) these rows stay, so admins can see who requested a code,
/// whether it was sent, and whether they ever got through.
/// </summary>
public class OtpEvent : BaseEntity
{
    public string MobileNumber { get; set; } = string.Empty;
    public VerificationPurpose Purpose { get; set; }
    public OtpEventType Type { get; set; }
    /// <summary>Error text for SendFailed; otherwise usually null.</summary>
    public string? Detail { get; set; }
    /// <summary>Green API idMessage for CodeSent.</summary>
    public string? ProviderMessageId { get; set; }
}
