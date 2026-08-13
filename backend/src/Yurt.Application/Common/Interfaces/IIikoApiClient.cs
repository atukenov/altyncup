namespace Yurt.Application.Common.Interfaces;

/// <summary>
/// Low-level client for the iikoCloud (iikoTransport) API.
/// All calls throw <see cref="IikoApiException"/> on transport or API errors —
/// callers decide whether loyalty failures are fatal for their flow.
/// </summary>
public interface IIikoApiClient
{
    /// <summary>Get customer info (incl. wallet balances) by phone. Null if not registered in iiko.</summary>
    Task<IikoCustomerInfo?> GetCustomerByPhoneAsync(string phone, CancellationToken ct = default);

    /// <summary>Create or update the iiko customer record. Returns the iiko customer id.</summary>
    Task<Guid> CreateOrUpdateCustomerAsync(
        string phone, string? name, string? surname, CancellationToken ct = default);

    /// <summary>Enroll the customer into the configured loyalty program. Returns the program wallet id.</summary>
    Task<Guid> AddCustomerToProgramAsync(Guid iikoCustomerId, CancellationToken ct = default);

    /// <summary>Credit (refill) the customer's wallet balance.</summary>
    Task TopupAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default);

    /// <summary>Debit (withdraw) from the customer's wallet balance.</summary>
    Task ChargeoffAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default);

    /// <summary>Hold (reserve) an amount on the customer's wallet. Returns the hold transaction id.</summary>
    Task<Guid> HoldAsync(Guid iikoCustomerId, Guid walletId, decimal sum, string? comment, CancellationToken ct = default);

    /// <summary>Release a previously placed hold.</summary>
    Task CancelHoldAsync(Guid holdTransactionId, CancellationToken ct = default);
}

public record IikoWalletBalance(Guid Id, string? Name, int Type, decimal Balance);

public record IikoCustomerInfo(Guid Id, string? Phone, List<IikoWalletBalance> WalletBalances);

public class IikoApiException : Exception
{
    public int? StatusCode { get; }

    public IikoApiException(string message, int? statusCode = null, Exception? inner = null)
        : base(message, inner) => StatusCode = statusCode;
}
