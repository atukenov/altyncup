namespace Yurt.Domain.Enums;

public enum OrderStatus
{
    Created = 0,
    Accepted = 1,
    Preparing = 2,
    Ready = 3,
    Completed = 4,
    Declined = 5
}

public enum PaymentStatus
{
    Unpaid = 0,
    Paid = 1,
    Refunded = 2
}

public enum PaymentProvider
{
    KaspiSandbox = 0,
    AiPay = 1,
    PayBot = 2,
    Halyk = 3,
    FreedomPay = 4,
    Stripe = 5,
    PayPal = 6
}

public enum Currency
{
    KZT = 0,
    USD = 1,
    EUR = 2
}

public enum PaymentRecordStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2,
    Expired = 3,
    Refunded = 4
}

public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
    Other = 2,
    KaspiBank = 3,
    HalykBank = 4,
    FreedomBank = 5
}

public enum AdminRole
{
    Admin = 0,
    Worker = 1
}

public enum DiscountType
{
    Percentage = 0,
    FixedAmount = 1
}
