namespace Yurt.Application.Features.Auth.DTOs;

public record AuthResponseDto(string Token, string UserType, Guid UserId, string DisplayName);

public record CustomerRegisterDto(string MobileNumber, string Pin4, string FirstName = "", string LastName = "");
public record CustomerLoginDto(string MobileNumber, string Pin4);
public record AdminLoginDto(string Username, string Password);
public record UpdateProfileDto(string FirstName, string LastName);

public record CustomerProfileDto(Guid Id, string MobileNumber, string FirstName, string LastName, DateTime CreatedAt);
