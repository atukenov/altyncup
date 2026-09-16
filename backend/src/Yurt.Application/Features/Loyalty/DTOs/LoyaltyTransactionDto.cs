namespace Yurt.Application.Features.Loyalty.DTOs;

/// <summary>One entry in the customer's bonus-wallet ledger — an offsite (in-shop counter) purchase.</summary>
/// <param name="WhenCreated">When the transaction happened, in UTC.</param>
/// <param name="Sum">Signed bonus amount (positive = earned, negative = spent).</param>
/// <param name="TypeName">iiko's human-readable transaction type label.</param>
/// <param name="OrderNumber">POS order number, when the transaction is tied to a sale.</param>
/// <param name="BalanceAfter">Wallet balance immediately after this transaction, if iiko reported it.</param>
public record LoyaltyTransactionDto(
    DateTime WhenCreated,
    decimal Sum,
    string? TypeName,
    int? OrderNumber,
    decimal? BalanceAfter);

/// <param name="Enabled">Loyalty feature flag is on.</param>
/// <param name="Available">iiko responded — when false the client should show a "temporarily unavailable" state.</param>
/// <param name="Linked">Customer is linked to an iiko wallet.</param>
/// <param name="Transactions">Offsite transactions only — app-order earn/spend already appears in order history.</param>
public record LoyaltyHistoryDto(
    bool Enabled,
    bool Available,
    bool Linked,
    List<LoyaltyTransactionDto> Transactions);
