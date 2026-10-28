using Yurt.Application.Features.Auth;

namespace Yurt.Application.Features.Auth.DTOs;

public record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    string UserType,
    Guid UserId,
    string DisplayName,
    string? Role = null,
    bool MustChangePassword = false);

public record RefreshRequestDto(string RefreshToken);

public record CustomerRegisterDto(string MobileNumber, string Pin4, string FirstName = "", string LastName = "");
/// <summary>
/// Send either the WhatsApp <paramref name="Code"/> or, when the start response said
/// <c>provider: "firebase"</c>, the Firebase ID token the app got after confirming the SMS code.
/// </summary>
public record RegisterVerifyDto(string MobileNumber, string? Code = null, string? FirebaseIdToken = null);

/// <summary>
/// <paramref name="Provider"/> is <c>"whatsapp"</c> (the backend sent a code) or
/// <c>"firebase"</c> (the app must send the SMS itself); <paramref name="CodeLength"/> is how
/// many code boxes to show.
/// </summary>
public record RegistrationStartResponseDto(
    string MobileNumber,
    DateTime ExpiresAt,
    string? DevCode = null,
    string Provider = RegistrationStartResponseDto.WhatsApp,
    int CodeLength = OtpOptions.WhatsAppCodeLength)
{
    public const string WhatsApp = "whatsapp";
    public const string Firebase = "firebase";
}
public record CustomerLoginDto(string MobileNumber, string Pin4);
public record AdminLoginDto(string Username, string Password);
public record UpdateProfileDto(string FirstName, string LastName, DateOnly? DateOfBirth = null);
public record ChangeMobileNumberDto(string MobileNumber);

public record CustomerProfileDto(Guid Id, string MobileNumber, string FirstName, string LastName, DateTime CreatedAt, DateOnly? DateOfBirth = null, bool IsPhoneVerified = false);
public record VerifyPhoneDto(string? Code = null, string? FirebaseIdToken = null);
public record ChangePinDto(string CurrentPin, string NewPin);
public record PinResetStartDto(string MobileNumber);
public record PinResetVerifyDto(string MobileNumber, string? Code, string NewPin4, string? FirebaseIdToken = null);
public record ChangeAdminPasswordDto(string CurrentPassword, string NewPassword);
public record RegisterDeviceTokenDto(string Token, string Platform);
