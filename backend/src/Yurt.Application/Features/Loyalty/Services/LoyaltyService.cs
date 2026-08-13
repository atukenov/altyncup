using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Loyalty.DTOs;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Loyalty.Services;

public class LoyaltyService
{
    private readonly IApplicationDbContext _db;
    private readonly IIikoApiClient _iiko;
    private readonly IikoOptions _options;
    private readonly IAuditLogService _audit;
    private readonly ILogger<LoyaltyService> _logger;

    public LoyaltyService(
        IApplicationDbContext db,
        IIikoApiClient iiko,
        IikoOptions options,
        IAuditLogService audit,
        ILogger<LoyaltyService> logger)
    {
        _db = db;
        _iiko = iiko;
        _options = options;
        _audit = audit;
        _logger = logger;
    }

    public static decimal CalculatePoints(decimal orderTotal, decimal earnPercent)
        => orderTotal <= 0 || earnPercent <= 0
            ? 0m
            : Math.Round(orderTotal * earnPercent / 100m, 2, MidpointRounding.ToZero);

    /// <summary>Balance for the customer's profile/cart. Never throws — degrades to Available=false.</summary>
    public async Task<LoyaltyBalanceDto> GetBalanceAsync(Guid customerId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return new LoyaltyBalanceDto(false, false, false, null, 0);

        var user = await _db.CustomerUsers.FirstOrDefaultAsync(u => u.Id == customerId, ct);
        if (user == null)
            return new LoyaltyBalanceDto(true, false, false, null, _options.EarnPercent);

        try
        {
            await EnsureLinkedAsync(user, ct);

            var info = await _iiko.GetCustomerByPhoneAsync(user.MobileNumber, ct);
            if (info == null)
                return new LoyaltyBalanceDto(true, true, false, null, _options.EarnPercent);

            var balance = info.WalletBalances
                .Where(w => user.IikoWalletId == null || w.Id == user.IikoWalletId)
                .Sum(w => w.Balance);

            return new LoyaltyBalanceDto(true, true, true, balance, _options.EarnPercent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "iiko loyalty balance unavailable for customer {CustomerId}", customerId);
            return new LoyaltyBalanceDto(true, false, user.IikoCustomerId != null, null, _options.EarnPercent);
        }
    }

    /// <summary>
    /// Credit EarnPercent of the completed order total to the customer's iiko wallet.
    /// Idempotent per order; never throws — a loyalty failure must not fail order completion.
    /// </summary>
    public async Task CreditForOrderAsync(Order order, CancellationToken ct = default)
    {
        if (!_options.Enabled) return;
        if (order.LoyaltyPointsEarned != null) return; // already credited

        var points = CalculatePoints(order.Total, _options.EarnPercent);
        if (points <= 0) return;

        try
        {
            var user = order.CustomerUser
                ?? await _db.CustomerUsers.FirstOrDefaultAsync(u => u.Id == order.CustomerUserId, ct);
            if (user == null) return;

            await EnsureLinkedAsync(user, ct);

            await _iiko.TopupAsync(
                user.IikoCustomerId!.Value,
                user.IikoWalletId!.Value,
                points,
                $"Altyncup order {order.Id}",
                ct);

            order.LoyaltyPointsEarned = points;
            await _db.SaveChangesAsync(ct);

            await _audit.LogAsync("LoyaltyPointsCredited", "Order", order.Id.ToString(),
                $"{points} points ({_options.EarnPercent}%) to iiko customer {user.IikoCustomerId}", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to credit loyalty points for order {OrderId} (customer {CustomerId})",
                order.Id, order.CustomerUserId);
        }
    }

    /// <summary>Create/enroll the customer in iiko on first use and persist the link ids.</summary>
    private async Task EnsureLinkedAsync(CustomerUser user, CancellationToken ct)
    {
        if (user.IikoCustomerId != null && user.IikoWalletId != null) return;

        if (user.IikoCustomerId == null)
        {
            user.IikoCustomerId = await _iiko.CreateOrUpdateCustomerAsync(
                user.MobileNumber, user.FirstName, user.LastName, ct);
        }

        if (user.IikoWalletId == null)
        {
            user.IikoWalletId = await _iiko.AddCustomerToProgramAsync(user.IikoCustomerId.Value, ct);
        }

        await _db.SaveChangesAsync(ct);
    }
}
