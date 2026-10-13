namespace MiniFootballBooking.Models;

public class Banner
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề")]
    [StringLength(150)]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; } = string.Empty;

    [StringLength(250)]
    [Display(Name = "Phụ đề")]
    public string? Subtitle { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập đường dẫn ảnh")]
    [StringLength(300)]
    [Display(Name = "Ảnh")]
    public string ImageUrl { get; set; } = string.Empty;

    [StringLength(300)]
    [Display(Name = "Liên kết")]
    public string? LinkUrl { get; set; }

    [Display(Name = "Thứ tự")]
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
