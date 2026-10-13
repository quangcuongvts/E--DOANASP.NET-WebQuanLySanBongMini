namespace MiniFootballBooking.Models;

public class FootballField
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã sân")]
    [StringLength(20)]
    [Display(Name = "Mã sân")]
    public string FieldCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên sân")]
    [StringLength(150)]
    [Display(Name = "Tên sân")]
    public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn loại sân")]
    [Display(Name = "Loại sân")]
    public int FieldTypeId { get; set; }

    public FieldType? FieldType { get; set; }

    [StringLength(2000)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Range(2, 40)]
    [Display(Name = "Sức chứa")]
    public int Capacity { get; set; }

    [Range(5, 120)]
    [Display(Name = "Chiều dài (m)")]
    public decimal Length { get; set; }

    [Range(5, 90)]
    [Display(Name = "Chiều rộng (m)")]
    public decimal Width { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập loại mặt sân")]
    [StringLength(80)]
    [Display(Name = "Loại mặt sân")]
    public string SurfaceType { get; set; } = "Cỏ nhân tạo";

    [Range(0, 100000000)]
    [Display(Name = "Giá cơ bản / giờ")]
    public decimal BasePrice { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ")]
    [StringLength(250)]
    [Display(Name = "Địa chỉ")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập khu vực")]
    [StringLength(80)]
    [Display(Name = "Khu vực")]
    public string Area { get; set; } = string.Empty;

    [StringLength(300)]
    public string? ImageUrl { get; set; }

    [Display(Name = "Trạng thái")]
    public FieldStatus Status { get; set; } = FieldStatus.Available;

    [Display(Name = "Điểm đánh giá")]
    public decimal RatingAverage { get; set; }

    public bool HasLighting { get; set; } = true;
    public bool HasChangingRoom { get; set; } = true;
    public bool HasToilet { get; set; } = true;
    public bool HasParking { get; set; } = true;
    public bool HasWifi { get; set; }
    public bool HasWater { get; set; } = true;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<FieldImage> Images { get; set; } = new List<FieldImage>();
    public ICollection<PriceRule> PriceRules { get; set; } = new List<PriceRule>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Maintenance> Maintenances { get; set; } = new List<Maintenance>();
}
