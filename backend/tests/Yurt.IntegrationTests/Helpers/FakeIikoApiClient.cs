using Yurt.Application.Common.Interfaces;

namespace Yurt.IntegrationTests.Helpers;

/// <summary>In-memory stand-in for iikoCloud: records wallet operations, keeps a balance.</summary>
public class FakeIikoApiClient : IIikoApiClient
{
    public static readonly Guid CustomerId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    public static readonly Guid WalletId   = Guid.Parse("bbbbbbbb-1111-2222-3333-444444444444");

    private readonly Dictionary<string, decimal> _balancesByPhone = [];
    public readonly List<(Guid CustomerId, Guid WalletId, decimal Sum, string? Comment)> TopupCalls = [];
    private string? _lastPhone;

    public Task<IikoCustomerInfo?> GetCustomerByPhoneAsync(string phone, CancellationToken ct = default)
    {
        _balancesByPhone.TryGetValue(phone, out var balance);
        return Task.FromResult<IikoCustomerInfo?>(new IikoCustomerInfo(
            CustomerId, phone,
            [new IikoWalletBalance(WalletId, "Bonus", 1, balance)]));
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
        => Task.CompletedTask;

    public Task<Guid> HoldAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default)
        => Task.FromResult(Guid.NewGuid());

    public Task CancelHoldAsync(Guid holdTransactionId, CancellationToken ct = default)
        => Task.CompletedTask;
}
