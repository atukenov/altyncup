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

public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
    Other = 2
}

public enum AdminRole
{
    Admin = 0,
    Worker = 1
}
