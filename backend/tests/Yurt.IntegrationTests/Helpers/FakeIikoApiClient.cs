using Yurt.Application.Common.Interfaces;

namespace Yurt.IntegrationTests.Helpers;

/// <summary>
/// In-memory stand-in for iikoCloud: records wallet operations, keeps balances,
/// models holds (holds reduce the reported available balance, like real iiko),
/// and can inject failures per operation.
/// </summary>
public class FakeIikoApiClient : IIikoApiClient
{
    public static readonly Guid CustomerId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    public static readonly Guid WalletId   = Guid.Parse("bbbbbbbb-1111-2222-3333-444444444444");

    private readonly Dictionary<string, decimal> _balancesByPhone = [];
    private readonly Dictionary<Guid, decimal> _activeHolds = [];
    private string? _lastPhone;

    public readonly List<(Guid CustomerId, Guid WalletId, decimal Sum, string? Comment)> TopupCalls = [];
    public readonly List<(Guid CustomerId, Guid WalletId, decimal Sum, string? Comment)> ChargeoffCalls = [];
    public readonly List<(Guid CustomerId, Guid WalletId, decimal Sum, string? Comment)> HoldCalls = [];
    public readonly List<Guid> CancelHoldCalls = [];

    // Failure injection
    public bool FailCancelHold { get; set; }
    public bool FailChargeoff { get; set; }

    public int ActiveHoldCount => _activeHolds.Count;

    public void SetBalance(string phone, decimal balance)
    {
        _balancesByPhone[phone] = balance;
        _lastPhone = phone;
    }

    public Task<IikoCustomerInfo?> GetCustomerByPhoneAsync(string phone, CancellationToken ct = default)
    {
        _balancesByPhone.TryGetValue(phone, out var balance);
        var available = balance - _activeHolds.Values.Sum();
        return Task.FromResult<IikoCustomerInfo?>(new IikoCustomerInfo(
            CustomerId, phone,
            [new IikoWalletBalance(WalletId, "Bonus", 1, available)]));
    }

    public Task<Guid> CreateOrUpdateCustomerAsync(
        string phone, string? name, string? surname, CancellationToken ct = default)
    {
        _lastPhone = phone;
        _balancesByPhone.TryAdd(phone, 0);
        return Task.FromResult(CustomerId);
    }

    public Task<Guid> AddCustomerToProgramAsync(Guid iikoCustomerId, CancellationToken ct = default)
        => Task.FromResult(WalletId);

    public Task TopupAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
    {
        TopupCalls.Add((iikoCustomerId, walletId, sum, comment));
        if (_lastPhone != null)
            _balancesByPhone[_lastPhone] = _balancesByPhone.GetValueOrDefault(_lastPhone) + sum;
        return Task.CompletedTask;
    }

    public Task ChargeoffAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
    {
        if (FailChargeoff) throw new IikoApiException("chargeoff failed (injected)");
        ChargeoffCalls.Add((iikoCustomerId, walletId, sum, comment));
        if (_lastPhone != null)
            _balancesByPhone[_lastPhone] = _balancesByPhone.GetValueOrDefault(_lastPhone) - sum;
        return Task.CompletedTask;
    }

    public Task<Guid> HoldAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
    {
        HoldCalls.Add((iikoCustomerId, walletId, sum, comment));
        var id = Guid.NewGuid();
        _activeHolds[id] = sum;
        return Task.FromResult(id);
    }

    public Task CancelHoldAsync(Guid holdTransactionId, CancellationToken ct = default)
    {
        if (FailCancelHold) throw new IikoApiException("cancel_hold failed (injected)");
        CancelHoldCalls.Add(holdTransactionId);
        _activeHolds.Remove(holdTransactionId);
        return Task.CompletedTask;
    }
}
