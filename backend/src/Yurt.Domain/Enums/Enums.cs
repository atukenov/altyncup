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
    FreedomBank = 5,
    ApplePay = 6
}

/// <summary>Staff role. SuperAdmin sees everything, including IT sections (app releases, audit log,
/// iiko integration, admin accounts); Admin runs the cafe; Worker handles live orders only.</summary>
public enum AdminRole
{
    Admin = 0,
    Worker = 1,
    SuperAdmin = 2
}

public enum DiscountType
{
    Percentage = 0,
    FixedAmount = 1
}

/// <summary>What a <see cref="Entities.PhoneVerification"/> row is for — disambiguates rows for
/// the same destination phone number requested by two different flows (a new signup vs. an
/// existing customer changing their number).</summary>
public enum VerificationPurpose
{
    Registration = 0,
    PhoneChange = 1,
    PinReset = 2,
    PhoneVerify = 3
}

/// <summary>Whether an order has been pushed to iiko as a real delivery order (reporting/kitchen-routing side-channel; never drives customer-facing OrderStatus).</summary>
public enum IikoOrderSyncStatus
{
    NotPushed = 0,
    Pushed = 1,
    Closed = 2,
    Failed = 3,
    SkippedUnmapped = 4
}

/// <summary>Outstanding iiko order-sync operation, retried in the background when iiko was unavailable. Mirrors LoyaltyPendingAction's retry shape.</summary>
public enum IikoOrderSyncPendingAction
{
    None = 0,
    Push = 1,
    Close = 2
}

/// <summary>Which native push transport a <see cref="Entities.CustomerDeviceToken"/> was registered through — both resolve to FCM tokens, but iOS routes through APNs first.</summary>
public enum DevicePlatform
{
    Ios = 0,
    Android = 1
}

/// <summary>What happened to a one-time code (see <see cref="Entities.OtpEvent"/>).</summary>
public enum OtpEventType
{
    /// <summary>Green API accepted the message (queued; not proof of delivery).</summary>
    CodeSent = 0,
    /// <summary>Green API rejected the request or could not be reached.</summary>
    SendFailed = 1,
    /// <summary>Otp:Enabled is false, so no message was sent.</summary>
    DeliveryDisabled = 2,
    Verified = 3,
    WrongCode = 4,
    Expired = 5,
    TooManyAttempts = 6,
    /// <summary>Green API accepted the message but WhatsApp later reported it failed or the number has no WhatsApp.</summary>
    DeliveryFailed = 7
}
