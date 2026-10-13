using MiniFootballBooking.Data;

namespace MiniFootballBooking.Services;

public interface IPaymentService
{
    Task<ServiceResult<Payment>> ConfirmAsync(int bookingId, string actorId, PaymentMethod method, bool success, string? note);
}

public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;

    public PaymentService(ApplicationDbContext db, INotificationService notifications, IAuditService audit)
    {
        _db = db;
        _notifications = notifications;
        _audit = audit;
    }

    public async Task<ServiceResult<Payment>> ConfirmAsync(int bookingId, string actorId, PaymentMethod method, bool success, string? note)
    {
        var booking = await _db.Bookings
            .Include(b => b.Payments)
            .Include(b => b.Field)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
        {
            return ServiceResult<Payment>.Fail("Không tìm thấy đơn đặt sân.");
        }

        if (booking.Status is BookingStatus.Cancelled or BookingStatus.NoShow)
        {
            return ServiceResult<Payment>.Fail("Đơn đã hủy, không thể thanh toán.");
        }

        var payment = booking.Payments.OrderByDescending(p => p.Id).FirstOrDefault(p => p.PaymentStatus != PaymentStatus.Paid && p.PaymentStatus != PaymentStatus.Refunded)
                      ?? booking.Payments.OrderByDescending(p => p.Id).FirstOrDefault();

        if (payment == null)
        {
            payment = new Payment
            {
                PaymentCode = await NextPaymentCodeAsync(),
                BookingId = booking.Id,
                UserId = booking.CustomerId,
                Amount = booking.TotalAmount
            };
            _db.Payments.Add(payment);
        }

        if (!success)
        {
            payment.PaymentStatus = PaymentStatus.Failed;
            payment.PaymentMethod = method;
            payment.Note = note ?? "Thanh toán thất bại";
            booking.PaymentStatus = PaymentStatus.Failed;
            booking.UpdatedAt = AppClock.Now;
            await _db.SaveChangesAsync();
            await _audit.LogAsync(actorId, "PAYMENT", "Payment", payment.Id.ToString(), null, new { payment.PaymentCode, Status = "Failed" });
            return ServiceResult<Payment>.Fail("Thanh toán không thành công. Bạn có thể thử lại.");
        }

        var alreadyPaid = booking.Payments.Where(p => p.Id != payment.Id && p.PaymentStatus == PaymentStatus.Paid).Sum(p => p.Amount);
        var due = Math.Max(0, booking.TotalAmount - alreadyPaid);
        if (due == 0)
        {
            return ServiceResult<Payment>.Ok(payment, "Đơn đã được thanh toán.");
        }

        payment.Amount = due;
        payment.PaymentStatus = PaymentStatus.Paid;
        payment.PaymentMethod = method;
        payment.PaymentDate = AppClock.Now;
        payment.TransactionCode = method == PaymentMethod.VNPay
            ? "VNP" + AppClock.Now.ToString("yyyyMMddHHmmss")
            : "TX" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        payment.Note = note ?? payment.Note;
        payment.UserId = booking.CustomerId;
        booking.PaymentStatus = alreadyPaid + due >= booking.TotalAmount ? PaymentStatus.Paid : PaymentStatus.PartiallyPaid;

        if (booking.Status == BookingStatus.Pending)
        {
            booking.Status = BookingStatus.Confirmed;
        }

        booking.UpdatedAt = AppClock.Now;
        await _db.SaveChangesAsync();

        await _notifications.NotifyAsync(
            booking.CustomerId,
            "Thanh toán thành công",
            $"Đơn {booking.BookingCode} đã thanh toán {payment.Amount.VndText()}.",
            NotificationType.PaymentSuccess,
            $"/Booking/Details/{booking.Id}");

        if (booking.Status == BookingStatus.Confirmed)
        {
            await _notifications.NotifyAsync(
                booking.CustomerId,
                "Booking đã xác nhận",
                $"{booking.Field?.Name} ngày {booking.BookingDate:dd/MM/yyyy} {DisplayNames.Hours(booking.StartTime, booking.EndTime)}.",
                NotificationType.BookingConfirmed,
                $"/Booking/Details/{booking.Id}");
        }

        await _audit.LogAsync(actorId, "PAYMENT", "Payment", payment.Id.ToString(), null, new
        {
            payment.PaymentCode,
            payment.Amount,
            Method = method.ToString(),
            Status = payment.PaymentStatus.ToString(),
            booking.BookingCode
        });

        return ServiceResult<Payment>.Ok(payment, "Thanh toán thành công.");
    }

    private async Task<string> NextPaymentCodeAsync()
    {
        var prefix = "PM" + AppClock.Today.ToString("yyyyMMdd");
        var last = await _db.Payments.Where(p => p.PaymentCode.StartsWith(prefix))
            .OrderByDescending(p => p.PaymentCode)
            .Select(p => p.PaymentCode)
            .FirstOrDefaultAsync();
        var seq = 1;
        if (last != null && int.TryParse(last[^4..], out var number))
        {
            seq = number + 1;
        }

        return prefix + seq.ToString("D4");
    }
}
