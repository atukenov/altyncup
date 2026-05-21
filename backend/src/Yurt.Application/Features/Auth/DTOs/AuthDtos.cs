namespace Yurt.Application.Features.Auth.DTOs;

public record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    string UserType,
    Guid UserId,
    string DisplayName,
    string? Role = null);

public record RefreshRequestDto(string RefreshToken);

public record CustomerRegisterDto(string MobileNumber, string Pin4, string FirstName = "", string LastName = "");
public record CustomerLoginDto(string MobileNumber, string Pin4);
public record AdminLoginDto(string Username, string Password);
public record UpdateProfileDto(string FirstName, string LastName);

public record CustomerProfileDto(Guid Id, string MobileNumber, string FirstName, string LastName, DateTime CreatedAt);
public record ChangePinDto(string CurrentPin, string NewPin);
