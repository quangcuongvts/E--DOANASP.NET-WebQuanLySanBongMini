namespace MiniFootballBooking.Models;

public class Booking
{
    public int Id { get; set; }

    [Required]
    [StringLength(20)]
    public string BookingCode { get; set; } = string.Empty;

    [Required]
    public string CustomerId { get; set; } = string.Empty;

    public ApplicationUser? Customer { get; set; }

    public int FieldId { get; set; }
    public FootballField? Field { get; set; }

    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public decimal OriginalAmount { get; set; }
    public decimal ServiceAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public int RefundPercent { get; set; }

    public int? VoucherId { get; set; }
    public Voucher? Voucher { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    [StringLength(500)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    [StringLength(500)]
    public string? CancellationReason { get; set; }

    public string? CancelledById { get; set; }
    public ApplicationUser? CancelledBy { get; set; }

    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }

    [StringLength(300)]
    public string? CheckInNote { get; set; }

    public string? StaffId { get; set; }
    public ApplicationUser? Staff { get; set; }

    public ICollection<BookingServiceLine> ServiceLines { get; set; } = new List<BookingServiceLine>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}
