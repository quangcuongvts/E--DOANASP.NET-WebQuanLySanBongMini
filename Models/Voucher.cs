namespace MiniFootballBooking.Models;

public class Voucher
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã voucher")]
    [StringLength(30)]
    [Display(Name = "Mã")]
    public string VoucherCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên voucher")]
    [StringLength(120)]
    [Display(Name = "Tên")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Display(Name = "Loại giảm")]
    public DiscountType DiscountType { get; set; }

    [Range(0, 100000000)]
    [Display(Name = "Giá trị giảm")]
    public decimal DiscountValue { get; set; }

    [Range(0, 100000000)]
    [Display(Name = "Đơn tối thiểu")]
    public decimal MinOrderAmount { get; set; }

    [Display(Name = "Giảm tối đa")]
    public decimal? MaxDiscountAmount { get; set; }

    [Display(Name = "Bắt đầu")]
    public DateTime StartDate { get; set; }

    [Display(Name = "Kết thúc")]
    public DateTime EndDate { get; set; }

    [Range(1, 1000000)]
    [Display(Name = "Giới hạn lượt dùng")]
    public int UsageLimit { get; set; }

    public int UsedCount { get; set; }

    [Range(1, 100)]
    [Display(Name = "Lượt dùng / khách")]
    public int MaxUsagePerUser { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<VoucherUsage> Usages { get; set; } = new List<VoucherUsage>();
}
