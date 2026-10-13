namespace MiniFootballBooking.Models;

public class Payment
{
    public int Id { get; set; }

    [Required]
    [StringLength(20)]
    public string PaymentCode { get; set; } = string.Empty;

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    [Range(0, 1000000000)]
    public decimal Amount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    [StringLength(50)]
    public string? TransactionCode { get; set; }

    public DateTime? PaymentDate { get; set; }

    [StringLength(300)]
    public string? Note { get; set; }
}
