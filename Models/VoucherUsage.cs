namespace MiniFootballBooking.Models;

public class VoucherUsage
{
    public int Id { get; set; }
    public int VoucherId { get; set; }
    public Voucher? Voucher { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public int BookingId { get; set; }
    public Booking? Booking { get; set; }
    public DateTime UsedAt { get; set; }
}
