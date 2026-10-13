namespace MiniFootballBooking.ViewModels;

public class AdminDashboardViewModel
{
    public bool IsStaffDashboard { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal RevenueChangePercent { get; set; }
    public decimal TodayRevenue { get; set; }
    public decimal ShiftRevenue { get; set; }
    public int TodayBookings { get; set; }
    public int MonthBookings { get; set; }
    public int TotalCustomers { get; set; }
    public int NewCustomers { get; set; }
    public int TotalFields { get; set; }
    public int FieldsInUse { get; set; }
    public int FreeFields { get; set; }
    public int CancelledBookings { get; set; }
    public int PendingBookings { get; set; }
    public int CheckedInCustomers { get; set; }
    public decimal FillRate { get; set; }
    public List<string> Last7Labels { get; set; } = [];
    public List<decimal> Last7Revenue { get; set; } = [];
    public List<string> MonthLabels { get; set; } = [];
    public List<decimal> MonthRevenue { get; set; } = [];
    public List<string> StatusLabels { get; set; } = [];
    public List<int> StatusCounts { get; set; } = [];
    public List<string> FieldLabels { get; set; } = [];
    public List<int> FieldBookingCounts { get; set; } = [];
    public List<string> TopFieldLabels { get; set; } = [];
    public List<int> TopFieldCounts { get; set; } = [];
    public List<BookingListItemViewModel> TodaySchedule { get; set; } = [];
    public List<BookingListItemViewModel> RecentBookings { get; set; } = [];
}

public class ReportViewModel
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int? FieldId { get; set; }
    public int? FieldTypeId { get; set; }
    public string Focus { get; set; } = "revenue";
    public int TotalBookings { get; set; }
    public int CompletedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public decimal Revenue { get; set; }
    public decimal AverageBookingValue { get; set; }
    public decimal CancelRate { get; set; }
    public decimal FillRate { get; set; }
    public int NewCustomers { get; set; }
    public List<FootballField> Fields { get; set; } = [];
    public List<FieldType> Types { get; set; } = [];
    public List<NamedStat> PopularFields { get; set; } = [];
    public List<NamedStat> PopularHours { get; set; } = [];
    public List<NamedStat> PopularServices { get; set; } = [];
    public List<NamedStat> TopCustomers { get; set; } = [];
    public List<decimal> RevenueSeries { get; set; } = [];
    public List<string> RevenueLabels { get; set; } = [];
    public List<BookingListItemViewModel> Bookings { get; set; } = [];
    public PagingModel Paging { get; set; } = new();
}

public class NamedStat
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

public class FieldFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã sân")]
    [StringLength(20)]
    public string FieldCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên sân")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn loại sân")]
    public int FieldTypeId { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(2, 40)]
    public int Capacity { get; set; }

    [Range(5, 120)]
    public decimal Length { get; set; }

    [Range(5, 90)]
    public decimal Width { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập loại mặt sân")]
    [StringLength(80)]
    public string SurfaceType { get; set; } = "Cỏ nhân tạo";

    [Range(0, 100000000)]
    public decimal BasePrice { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ")]
    [StringLength(250)]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập khu vực")]
    [StringLength(80)]
    public string Area { get; set; } = string.Empty;

    public FieldStatus Status { get; set; } = FieldStatus.Available;
    public bool HasLighting { get; set; } = true;
    public bool HasChangingRoom { get; set; } = true;
    public bool HasToilet { get; set; } = true;
    public bool HasParking { get; set; } = true;
    public bool HasWifi { get; set; }
    public bool HasWater { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string? ImageUrl { get; set; }
    public List<FieldImage> Images { get; set; } = [];
    public List<FieldType> Types { get; set; } = [];
}

public class UserFormViewModel
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [RegularExpression(@"^0[0-9]{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ")]
    public string Phone { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }
    public Gender Gender { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn vai trò")]
    public string Role { get; set; } = "Customer";

    public bool IsActive { get; set; } = true;

    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    public string? AvatarUrl { get; set; }
}

public class UserRowViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserListViewModel
{
    public List<UserRowViewModel> Items { get; set; } = [];
    public PagingModel Paging { get; set; } = new();
    public string? Keyword { get; set; }
    public string? Role { get; set; }
    public string? Status { get; set; }
}

public class SchedulePageViewModel
{
    public DateOnly Date { get; set; }
    public int? FieldId { get; set; }
    public string Mode { get; set; } = "day";
    public List<FootballField> Fields { get; set; } = [];
    public List<FieldScheduleDto> Schedules { get; set; } = [];
    public List<WeekDayColumn> Week { get; set; } = [];
    public List<MonthCell> Month { get; set; } = [];
    public DateOnly MonthStart { get; set; }
}

public class WeekDayColumn
{
    public DateOnly Date { get; set; }
    public int BookingCount { get; set; }
    public List<BookingListItemViewModel> Items { get; set; } = [];
}

public class MonthCell
{
    public DateOnly Date { get; set; }
    public bool InMonth { get; set; }
    public int BookingCount { get; set; }
}

public class RolePageViewModel
{
    public List<RoleStatViewModel> Roles { get; set; } = [];
    public List<UserRowViewModel> Users { get; set; } = [];
}

public class RoleStatViewModel
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}
