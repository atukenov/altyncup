using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Loyalty.DTOs;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

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

    /// <summary>
    /// Balance for the customer's profile/cart. Never throws — degrades to Available=false.
    /// With <paramref name="linkIfMissing"/> false (admin views), an unlinked customer is
    /// reported as Linked=false instead of being created in iiko as a side effect.
    /// </summary>
    public async Task<LoyaltyBalanceDto> GetBalanceAsync(
        Guid customerId, bool linkIfMissing = true, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return new LoyaltyBalanceDto(false, false, false, null, 0);

        var user = await _db.CustomerUsers.FirstOrDefaultAsync(u => u.Id == customerId, ct);
        if (user == null)
            return new LoyaltyBalanceDto(true, false, false, null, _options.EarnPercent);

        try
        {
            if (user.IikoCustomerId == null)
            {
                if (linkIfMissing)
                    await EnsureLinkedAsync(user, ct);
                else
                    await SelfHealLinkAsync(user, ct);
            }

            if (user.IikoCustomerId == null)
                return new LoyaltyBalanceDto(true, true, false, null, _options.EarnPercent);

            var info = await _iiko.GetCustomerByPhoneAsync(user.MobileNumber, ct);
            if (info == null)
                return new LoyaltyBalanceDto(true, true, false, null, _options.EarnPercent);

            await SelfHealWalletIdAsync(user, info, ct);

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

        // Earn only on the money portion — the part paid with points earns nothing
        var earnBase = order.Total - (order.LoyaltyPointsSpent ?? 0m);
        var points = CalculatePoints(earnBase, _options.EarnPercent);
        if (points <= 0) return;

        try
        {
            var user = order.CustomerUser
                ?? await _db.CustomerUsers.FirstOrDefaultAsync(u => u.Id == order.CustomerUserId, ct);
            if (user == null) return;

            await EnsureLinkedAsync(user, ct);

            var info = await _iiko.GetCustomerByPhoneAsync(user.MobileNumber, ct);
            if (info != null) await SelfHealWalletIdAsync(user, info, ct);

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

    /// <summary>
    /// Reserve points as (partial) payment for a freshly placed order.
    /// Clamps the requested amount to min(balance, order total). Returns the points
    /// actually applied; 0 when disabled, invalid, or iiko is unavailable (fail open
    /// to normal payment — order placement must never fail because of loyalty).
    /// </summary>
    public async Task<decimal> TryHoldForOrderAsync(
        Order order, decimal requestedPoints, CancellationToken ct = default)
    {
        if (!_options.Enabled || requestedPoints <= 0) return 0m;

        try
        {
            var user = order.CustomerUser
                ?? await _db.CustomerUsers.FirstOrDefaultAsync(u => u.Id == order.CustomerUserId, ct);
            if (user == null) return 0m;

            await EnsureLinkedAsync(user, ct);

            var info = await _iiko.GetCustomerByPhoneAsync(user.MobileNumber, ct);
            if (info != null) await SelfHealWalletIdAsync(user, info, ct);

            var balance = info?.WalletBalances
                .Where(w => user.IikoWalletId == null || w.Id == user.IikoWalletId)
                .Sum(w => w.Balance) ?? 0m;

            var applied = Math.Round(
                Math.Min(requestedPoints, Math.Min(balance, order.Total)),
                2, MidpointRounding.ToZero);
            if (applied <= 0) return 0m;

            var holdId = await _iiko.HoldAsync(
                user.IikoCustomerId!.Value, user.IikoWalletId!.Value, applied,
                $"Altyncup order {order.Id}", ct);

            order.LoyaltyPointsSpent = applied;
            order.LoyaltyHoldTransactionId = holdId;
            // Fully covered by points → nothing left to collect at the counter
            if (applied >= order.Total) order.PaymentStatus = PaymentStatus.Paid;
            await _db.SaveChangesAsync(ct);

            await _audit.LogAsync("LoyaltyPointsHeld", "Order", order.Id.ToString(),
                $"{applied} points held (hold {holdId})", ct);
            return applied;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Loyalty hold failed for order {OrderId}; falling back to normal payment", order.Id);
            return 0m;
        }
    }

    /// <summary>
    /// Consume the points reserved for a completed order: release the hold, then
    /// charge off the amount. (iiko holds reduce the available balance, so charging
    /// off while the hold is active would debit the customer twice — release first.)
    /// Failures set LoyaltyPendingAction for background retry; never throws.
    /// </summary>
    public async Task FinalizeSpendForOrderAsync(Order order, CancellationToken ct = default)
    {
        if (!_options.Enabled) return;
        if (order.LoyaltyPointsSpent is not > 0) return;
        if (order.LoyaltyHoldTransactionId == null &&
            order.LoyaltyPendingAction != LoyaltyPendingAction.ChargeOff) return; // already finalized

        var user = order.CustomerUser
            ?? await _db.CustomerUsers.FirstOrDefaultAsync(u => u.Id == order.CustomerUserId, ct);
        if (user?.IikoCustomerId == null || user.IikoWalletId == null) return;

        if (order.LoyaltyHoldTransactionId != null)
        {
            try
            {
                await _iiko.CancelHoldAsync(order.LoyaltyHoldTransactionId.Value, ct);
                order.LoyaltyHoldTransactionId = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to release hold for completed order {OrderId}; will retry", order.Id);
                order.LoyaltyPendingAction = LoyaltyPendingAction.FinalizeSpend;
                await _db.SaveChangesAsync(ct);
                return;
            }
        }

        try
        {
            await _iiko.ChargeoffAsync(
                user.IikoCustomerId.Value, user.IikoWalletId.Value,
                order.LoyaltyPointsSpent.Value, $"Altyncup order {order.Id}", ct);
            order.LoyaltyPendingAction = LoyaltyPendingAction.None;
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync("LoyaltyPointsSpent", "Order", order.Id.ToString(),
                $"{order.LoyaltyPointsSpent} points charged off", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chargeoff failed for completed order {OrderId}; will retry", order.Id);
            order.LoyaltyPendingAction = LoyaltyPendingAction.ChargeOff;
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Release the hold of a declined/cancelled order — the customer keeps their points.
    /// Failures set LoyaltyPendingAction.Release for background retry; never throws.
    /// </summary>
    public async Task ReleaseHoldForOrderAsync(Order order, CancellationToken ct = default)
    {
        if (!_options.Enabled) return;
        if (order.LoyaltyHoldTransactionId == null) return;

        try
        {
            await _iiko.CancelHoldAsync(order.LoyaltyHoldTransactionId.Value, ct);
            order.LoyaltyHoldTransactionId = null;
            order.LoyaltyPointsSpent = null; // nothing was spent
            order.LoyaltyPendingAction = LoyaltyPendingAction.None;
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync("LoyaltyHoldReleased", "Order", order.Id.ToString(), null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release hold for declined order {OrderId}; will retry", order.Id);
            order.LoyaltyPendingAction = LoyaltyPendingAction.Release;
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Background retry of wallet operations that failed while iiko was down.</summary>
    public async Task RetryPendingAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled) return;

        var pending = await _db.Orders
            .Include(o => o.CustomerUser)
            .Where(o => o.LoyaltyPendingAction != LoyaltyPendingAction.None)
            .OrderBy(o => o.UpdatedAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var order in pending)
        {
            switch (order.LoyaltyPendingAction)
            {
                case LoyaltyPendingAction.FinalizeSpend:
                case LoyaltyPendingAction.ChargeOff:
                    await FinalizeSpendForOrderAsync(order, ct);
                    break;
                case LoyaltyPendingAction.Release:
                    await ReleaseHoldForOrderAsync(order, ct);
                    break;
            }
        }
    }

    /// <summary>
    /// Admin-view self-heal for a customer with no cached iiko link: adopts an iiko
    /// customer that already exists for their phone number (enrolled through another
    /// channel, or linked before a data issue wiped the local ids) instead of leaving
    /// the link broken until they next place an order. Never creates a new iiko
    /// customer — viewing a profile must not have that side effect.
    /// </summary>
    private async Task SelfHealLinkAsync(CustomerUser user, CancellationToken ct)
    {
        var existing = await _iiko.GetCustomerByPhoneAsync(user.MobileNumber, ct);
        if (existing == null) return; // not registered in iiko — nothing to adopt

        user.IikoCustomerId = existing.Id;
        user.IikoWalletId = await ResolveWalletIdAsync(existing.Id, existing, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Self-healed iiko link for customer {CustomerId} -> iiko customer {IikoCustomerId}",
            user.Id, existing.Id);
        await _audit.LogAsync("LoyaltySelfHealLinked", "CustomerUser", user.Id.ToString(),
            $"Linked to existing iiko customer {existing.Id}", ct);
    }

    /// <summary>
    /// Repairs a cached wallet id that doesn't match any of the customer's real iiko
    /// wallets — e.g. records linked before a fix that made us prefer iiko's per-customer
    /// userWalletId over the shared, non-balance-holding program walletId returned by
    /// program/add.
    /// </summary>
    private async Task SelfHealWalletIdAsync(CustomerUser user, IikoCustomerInfo info, CancellationToken ct)
    {
        if (info.WalletBalances.Count == 0) return;
        if (info.WalletBalances.Any(w => w.Id == user.IikoWalletId)) return;

        _logger.LogInformation(
            "Repairing stale iiko wallet id for customer {CustomerId}", user.Id);
        user.IikoWalletId = await ResolveWalletIdAsync(user.IikoCustomerId!.Value, info, ct);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Picks the wallet id that actually holds the customer's balance. customer/info's
    /// walletBalances is authoritative when unambiguous (exactly one wallet) — trust it
    /// directly rather than re-querying program/add, whose response for an
    /// already-enrolled customer isn't reliably the same shape as for a fresh enrollment:
    /// it can echo back the shared, non-balance-holding program walletId instead of the
    /// customer's own userWalletId, which silently zeroes out the balance lookup. Only
    /// fall back to program/add when there's no wallet yet (needs real enrollment) or
    /// genuine ambiguity between multiple wallets.
    /// </summary>
    private async Task<Guid> ResolveWalletIdAsync(Guid iikoCustomerId, IikoCustomerInfo info, CancellationToken ct)
        => info.WalletBalances.Count == 1
            ? info.WalletBalances[0].Id
            : await _iiko.AddCustomerToProgramAsync(iikoCustomerId, ct);

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
