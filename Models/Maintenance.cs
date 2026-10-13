namespace MiniFootballBooking.Models;

public class Maintenance
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn sân")]
    [Display(Name = "Sân")]
    public int FieldId { get; set; }

    public FootballField? Field { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề")]
    [StringLength(150)]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Display(Name = "Bắt đầu")]
    public DateTime StartDateTime { get; set; }

    [Display(Name = "Kết thúc")]
    public DateTime EndDateTime { get; set; }

    [Range(0, 1000000000)]
    [Display(Name = "Chi phí")]
    public decimal Cost { get; set; }

    [Display(Name = "Trạng thái")]
    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Scheduled;

    public string? CreatedBy { get; set; }
    public ApplicationUser? Creator { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
