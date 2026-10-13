namespace MiniFootballBooking.Models;

public enum BookingStatus
{
    Pending = 0,
    Confirmed = 1,
    CheckedIn = 2,
    Playing = 3,
    Completed = 4,
    Cancelled = 5,
    NoShow = 6
}

public enum FieldStatus
{
    Available = 0,
    Occupied = 1,
    Maintenance = 2,
    Inactive = 3
}

public enum PaymentStatus
{
    Unpaid = 0,
    PartiallyPaid = 1,
    Paid = 2,
    Refunded = 3,
    Failed = 4
}

public enum PaymentMethod
{
    Cash = 0,
    BankTransfer = 1,
    QRCode = 2,
    VNPay = 3
}

public enum DiscountType
{
    Percentage = 0,
    FixedAmount = 1
}

public enum Gender
{
    Male = 0,
    Female = 1,
    Other = 2
}

public enum MaintenanceStatus
{
    Scheduled = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}

public enum NotificationType
{
    BookingCreated = 0,
    BookingConfirmed = 1,
    BookingCancelled = 2,
    PaymentSuccess = 3,
    UpcomingMatch = 4,
    NewReview = 5,
    System = 6
}

public enum ContactStatus
{
    New = 0,
    Processed = 1
}

public enum SlotState
{
    Available = 0,
    Booked = 1,
    Pending = 2,
    Maintenance = 3
}
