namespace MiniFootballBooking.Models;

public class FieldType
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên loại sân")]
    [StringLength(100)]
    [Display(Name = "Tên loại sân")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Range(2, 30, ErrorMessage = "Sức chứa từ 2 đến 30 người")]
    [Display(Name = "Sức chứa")]
    public int Capacity { get; set; }

    [Display(Name = "Đang dùng")]
    public bool IsActive { get; set; } = true;

    public ICollection<FootballField> Fields { get; set; } = new List<FootballField>();
}
