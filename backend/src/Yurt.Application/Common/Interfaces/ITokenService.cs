namespace Yurt.Application.Common.Interfaces;

public interface ITokenService
{
    string GenerateCustomerToken(Guid userId, string mobileNumber);
    string GenerateAdminToken(Guid adminId, string username, string role);
    string GenerateRefreshToken();
    int RefreshTokenDays { get; }
}
