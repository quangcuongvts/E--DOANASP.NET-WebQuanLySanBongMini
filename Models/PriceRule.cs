namespace MiniFootballBooking.Models;

public class PriceRule
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn sân")]
    [Display(Name = "Sân")]
    public int FieldId { get; set; }

    public FootballField? Field { get; set; }

    [Range(0, 6, ErrorMessage = "Ngày trong tuần không hợp lệ")]
    [Display(Name = "Ngày trong tuần")]
    public int DayOfWeek { get; set; }

    [Display(Name = "Giờ bắt đầu")]
    public TimeOnly StartTime { get; set; }

    [Display(Name = "Giờ kết thúc")]
    public TimeOnly EndTime { get; set; }

    [Range(0, 100000000, ErrorMessage = "Giá không hợp lệ")]
    [Display(Name = "Giá mỗi giờ")]
    public decimal PricePerHour { get; set; }

    [Display(Name = "Giờ cao điểm")]
    public bool IsPeakHour { get; set; }

    [Display(Name = "Hiệu lực từ")]
    public DateOnly? EffectiveFrom { get; set; }

    [Display(Name = "Hiệu lực đến")]
    public DateOnly? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;
}
