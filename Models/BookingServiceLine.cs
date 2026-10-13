using System.ComponentModel.DataAnnotations.Schema;

namespace MiniFootballBooking.Models;

[Table("BookingServices")]
public class BookingServiceLine
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking? Booking { get; set; }
    public int ServiceId { get; set; }
    public ServiceItem? Service { get; set; }

    [Range(1, 50)]
    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}
