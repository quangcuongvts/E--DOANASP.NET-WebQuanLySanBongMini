using System.ComponentModel.DataAnnotations.Schema;

namespace MiniFootballBooking.Models;

[Table("Services")]
public class ServiceItem
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên dịch vụ")]
    [StringLength(120)]
    [Display(Name = "Tên dịch vụ")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Range(0, 100000000, ErrorMessage = "Giá không hợp lệ")]
    [Display(Name = "Đơn giá")]
    public decimal Price { get; set; }

    [StringLength(300)]
    public string? ImageUrl { get; set; }

    [Display(Name = "Đang kinh doanh")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<BookingServiceLine> BookingLines { get; set; } = new List<BookingServiceLine>();
}
