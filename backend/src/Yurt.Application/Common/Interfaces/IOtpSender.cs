namespace Yurt.Application.Common.Interfaces;

/// <summary>Delivers a one-time verification code to a customer's mobile number.</summary>
public interface IOtpSender
{
    /// <summary>
    /// Send the code and return the provider's message id (null if it gave none).
    /// Throws <see cref="OtpSendException"/> when delivery fails.
    /// </summary>
    Task<string?> SendAsync(string mobileNumber, string code, CancellationToken ct = default);

    /// <summary>
    /// Ask the provider whether the number has a WhatsApp account. Returns null when that
    /// can't be determined (provider error, timeout), so callers can still try to send.
    /// Never throws for provider failures.
    /// </summary>
    Task<bool?> HasWhatsAppAsync(string mobileNumber, CancellationToken ct = default);
}

public class OtpSendException : Exception
{
    public OtpSendException(string message, Exception? inner = null) : base(message, inner) { }
}
