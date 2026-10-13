namespace MiniFootballBooking.ViewModels;

public class BookingPageViewModel
{
    public int? FieldId { get; set; }
    public DateOnly Date { get; set; }
    public string? Start { get; set; }
    public string? End { get; set; }
    public bool IsCounter { get; set; }
    public List<FieldOptionViewModel> Fields { get; set; } = [];
    public List<ServiceItem> Services { get; set; } = [];
}

public class FieldOptionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public string? ImageUrl { get; set; }
    public string Area { get; set; } = string.Empty;
}

public class BookingCreateViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn sân")]
    public int FieldId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày đá")]
    public DateOnly BookingDate { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn giờ bắt đầu")]
    public TimeOnly StartTime { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn giờ kết thúc")]
    public TimeOnly EndTime { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    [StringLength(30)]
    public string? VoucherCode { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    public List<ServiceLineInput> Services { get; set; } = [];

    public string? CustomerId { get; set; }

    [StringLength(100)]
    public string? WalkInName { get; set; }

    [RegularExpression(@"^$|^0[0-9]{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ")]
    public string? WalkInPhone { get; set; }
}

public class BookingListItemViewModel
{
    public int Id { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public decimal TotalAmount { get; set; }
    public BookingStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BookingListViewModel
{
    public List<BookingListItemViewModel> Items { get; set; } = [];
    public PagingModel Paging { get; set; } = new();
    public string? Filter { get; set; }
    public string? Keyword { get; set; }
    public BookingStatus? Status { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public int? FieldId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public List<FootballField> Fields { get; set; } = [];
}

public class BookingDetailViewModel
{
    public int Id { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string FieldCode { get; set; } = string.Empty;
    public int FieldId { get; set; }
    public string Area { get; set; } = string.Empty;
    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal ServiceAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public int RefundPercent { get; set; }
    public string? VoucherCode { get; set; }
    public BookingStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public string? Note { get; set; }
    public string? CancellationReason { get; set; }
    public string? CancelledByName { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string? CheckInNote { get; set; }
    public string? StaffName { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ServiceQuoteLine> Services { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
    public bool CanCancel { get; set; }
    public bool CanReschedule { get; set; }
    public bool CanReview { get; set; }
    public bool CanPay { get; set; }
    public bool IsStaffView { get; set; }
    public ReviewCardViewModel? Review { get; set; }
}

public class CancelBookingViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do hủy")]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class RescheduleViewModel
{
    public int Id { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public int FieldId { get; set; }
    public string FieldName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn ngày")]
    public DateOnly BookingDate { get; set; }

    [Required]
    public TimeOnly StartTime { get; set; }

    [Required]
    public TimeOnly EndTime { get; set; }
}

public class CheckInViewModel
{
    public int Id { get; set; }

    [StringLength(300)]
    public string? Note { get; set; }
}

public class AddServiceViewModel
{
    public int BookingId { get; set; }

    [Range(1, int.MaxValue)]
    public int ServiceId { get; set; }

    [Range(1, 20)]
    public int Quantity { get; set; } = 1;
}

public class CustomerLookupViewModel
{
    public bool Found { get; set; }
    public string? Id { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
