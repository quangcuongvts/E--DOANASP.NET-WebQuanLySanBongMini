namespace MiniFootballBooking.Models;

public class Review
{
    public int Id { get; set; }

    [Required]
    public string CustomerId { get; set; } = string.Empty;

    public ApplicationUser? Customer { get; set; }

    public int FieldId { get; set; }
    public FootballField? Field { get; set; }

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    [Range(1, 5, ErrorMessage = "Điểm đánh giá từ 1 đến 5")]
    public int Rating { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập nội dung đánh giá")]
    [StringLength(1000, ErrorMessage = "Nội dung tối đa 1000 ký tự")]
    public string Comment { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsVisible { get; set; } = true;
}
