namespace MiniFootballBooking.ViewModels;

public class HomeViewModel
{
    public List<FieldCardViewModel> FeaturedFields { get; set; } = [];
    public List<ServiceItem> Services { get; set; } = [];
    public List<Voucher> Vouchers { get; set; } = [];
    public List<ReviewCardViewModel> Reviews { get; set; } = [];
    public List<Post> Posts { get; set; } = [];
    public List<Banner> Banners { get; set; } = [];
    public int FieldCount { get; set; }
    public int CustomerCount { get; set; }
    public int BookingCount { get; set; }
    public string? LastSearch { get; set; }
}

public class FieldCardViewModel
{
    public int Id { get; set; }
    public string FieldCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public decimal BasePrice { get; set; }
    public decimal RatingAverage { get; set; }
    public int RatingCount { get; set; }
    public string? ImageUrl { get; set; }
    public string Area { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public FieldStatus Status { get; set; }
    public FieldStatus DisplayStatus { get; set; }
    public int BookingCount { get; set; }
    public bool HasLighting { get; set; }
    public bool HasParking { get; set; }
}

public class FieldFilterViewModel
{
    public string? Keyword { get; set; }
    public int? TypeId { get; set; }
    public DateOnly? Date { get; set; }
    public TimeOnly? Start { get; set; }
    public TimeOnly? End { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public FieldStatus? Status { get; set; }
    public string? Area { get; set; }
    public string Sort { get; set; } = "popular";
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalItems { get; set; }
    public List<FieldCardViewModel> Fields { get; set; } = [];
    public List<FieldType> Types { get; set; } = [];
    public List<string> Areas { get; set; } = [];
}

public class FieldDetailViewModel
{
    public int Id { get; set; }
    public string FieldCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public decimal Length { get; set; }
    public decimal Width { get; set; }
    public string SurfaceType { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public FieldStatus DisplayStatus { get; set; }
    public decimal RatingAverage { get; set; }
    public int RatingCount { get; set; }
    public bool HasLighting { get; set; }
    public bool HasChangingRoom { get; set; }
    public bool HasToilet { get; set; }
    public bool HasParking { get; set; }
    public bool HasWifi { get; set; }
    public bool HasWater { get; set; }
    public List<string> Images { get; set; } = [];
    public List<PriceRule> PriceRules { get; set; } = [];
    public List<ReviewCardViewModel> Reviews { get; set; } = [];
    public List<SlotDto> Slots { get; set; } = [];
    public DateOnly SelectedDate { get; set; }
}

public class ReviewCardViewModel
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public int FieldId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsVisible { get; set; } = true;
    public int BookingId { get; set; }
    public bool CanEdit { get; set; }
}

public class SlotDto
{
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
    public string Status { get; set; } = "available";
    public string StatusText { get; set; } = "Trống";
    public decimal Price { get; set; }
}

public class FieldScheduleDto
{
    public int FieldId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string FieldCode { get; set; } = string.Empty;
    public List<SlotDto> Slots { get; set; } = [];
}

public class PriceBreakdown
{
    public decimal TotalHours { get; set; }
    public decimal BasePrice { get; set; }
    public decimal PeakHourCharge { get; set; }
    public decimal WeekendCharge { get; set; }
    public decimal TotalPrice { get; set; }
    public List<PriceHourLine> Lines { get; set; } = [];
}

public class PriceHourLine
{
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsPeak { get; set; }
    public bool IsWeekend { get; set; }
}

public class QuoteRequest
{
    public int FieldId { get; set; }
    public string Date { get; set; } = string.Empty;
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
    public string? VoucherCode { get; set; }
    public List<ServiceLineInput> Services { get; set; } = [];
}

public class ServiceLineInput
{
    public int ServiceId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class QuoteResponse
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public decimal FieldAmount { get; set; }
    public decimal ServiceAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public decimal BasePrice { get; set; }
    public decimal PeakHourCharge { get; set; }
    public decimal WeekendCharge { get; set; }
    public List<PriceHourLine> Lines { get; set; } = [];
    public List<ServiceQuoteLine> ServiceLines { get; set; } = [];
}

public class ServiceQuoteLine
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Total { get; set; }
}

public class VoucherCheckRequest
{
    public string? Code { get; set; }
    public decimal OrderAmount { get; set; }
}

public class VoucherResult
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? VoucherId { get; set; }
    public decimal Discount { get; set; }
    public string? Code { get; set; }
}
