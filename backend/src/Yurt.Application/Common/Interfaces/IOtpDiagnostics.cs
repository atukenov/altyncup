namespace Yurt.Application.Common.Interfaces;

/// <summary>
/// Checks the WhatsApp OTP setup end to end (config, instance state, recipient, optional
/// test message) and returns every provider response, so delivery problems are visible.
/// </summary>
public interface IOtpDiagnostics
{
    Task<OtpDiagnosticsReport> RunAsync(string mobileNumber, bool sendTestMessage, CancellationToken ct = default);
}

/// <param name="Ok">True when this step found nothing that would stop delivery.</param>
/// <param name="Response">Raw provider response body (truncated), or the error.</param>
/// <param name="Hint">What to fix when <paramref name="Ok"/> is false.</param>
public record OtpDiagnosticsStep(string Name, bool Ok, int? HttpStatus, string? Response, string? Hint);

public record OtpDiagnosticsReport(
    bool Enabled,
    string BaseUrl,
    string IdInstance,
    bool TokenConfigured,
    string ChatId,
    IReadOnlyList<string> ConfigWarnings,
    IReadOnlyList<OtpDiagnosticsStep> Steps)
{
    public bool AllOk => ConfigWarnings.Count == 0 && Steps.All(s => s.Ok);
}
