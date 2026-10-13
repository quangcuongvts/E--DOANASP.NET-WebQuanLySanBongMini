using System.Globalization;

namespace MiniFootballBooking.Helpers;

public static class AppClock
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    public static DateOnly Today => DateOnly.FromDateTime(Now);

    public static TimeOnly Time => TimeOnly.FromDateTime(Now);

    public static bool IsWeekend(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}

public static class AppConstants
{
    public const int PageSize = 10;
    public const int OpenHour = 6;
    public const int CloseHour = 23;
    public const long MaxImageBytes = 2 * 1024 * 1024;
    public static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
}

public static class Money
{
    private static readonly NumberFormatInfo Vnd = new()
    {
        NumberGroupSeparator = ".",
        NumberDecimalSeparator = ",",
        NumberDecimalDigits = 0
    };

    public static string VndText(this decimal value) => value.ToString("N0", Vnd) + " ₫";

    public static string VndText(this decimal? value) => (value ?? 0m).VndText();

    public static decimal RoundMoney(decimal value) =>
        Math.Round(value / 1000m, 0, MidpointRounding.AwayFromZero) * 1000m;
}

public static class DisplayNames
{
    public static string Text(this BookingStatus status) => status switch
    {
        BookingStatus.Pending => "Chờ xác nhận",
        BookingStatus.Confirmed => "Đã xác nhận",
        BookingStatus.CheckedIn => "Đã check-in",
        BookingStatus.Playing => "Đang chơi",
        BookingStatus.Completed => "Hoàn thành",
        BookingStatus.Cancelled => "Đã hủy",
        BookingStatus.NoShow => "Không đến",
        _ => status.ToString()
    };

    public static string Css(this BookingStatus status) => status switch
    {
        BookingStatus.Pending => "st-pending",
        BookingStatus.Confirmed => "st-confirmed",
        BookingStatus.CheckedIn => "st-checkedin",
        BookingStatus.Playing => "st-playing",
        BookingStatus.Completed => "st-completed",
        BookingStatus.Cancelled => "st-cancelled",
        BookingStatus.NoShow => "st-noshow",
        _ => "st-completed"
    };

    public static string Text(this FieldStatus status) => status switch
    {
        FieldStatus.Available => "Còn trống",
        FieldStatus.Occupied => "Đang sử dụng",
        FieldStatus.Maintenance => "Bảo trì",
        FieldStatus.Inactive => "Ngừng hoạt động",
        _ => status.ToString()
    };

    public static string Css(this FieldStatus status) => status switch
    {
        FieldStatus.Available => "st-available",
        FieldStatus.Occupied => "st-playing",
        FieldStatus.Maintenance => "st-maintenance",
        FieldStatus.Inactive => "st-cancelled",
        _ => "st-completed"
    };

    public static string Text(this PaymentStatus status) => status switch
    {
        PaymentStatus.Unpaid => "Chưa thanh toán",
        PaymentStatus.PartiallyPaid => "Thanh toán một phần",
        PaymentStatus.Paid => "Đã thanh toán",
        PaymentStatus.Refunded => "Đã hoàn tiền",
        PaymentStatus.Failed => "Thất bại",
        _ => status.ToString()
    };

    public static string Css(this PaymentStatus status) => status switch
    {
        PaymentStatus.Unpaid => "st-pending",
        PaymentStatus.PartiallyPaid => "st-pending",
        PaymentStatus.Paid => "st-confirmed",
        PaymentStatus.Refunded => "st-maintenance",
        PaymentStatus.Failed => "st-cancelled",
        _ => "st-completed"
    };

    public static string Text(this PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Tiền mặt tại sân",
        PaymentMethod.BankTransfer => "Chuyển khoản",
        PaymentMethod.QRCode => "QR Code",
        PaymentMethod.VNPay => "VNPay (demo)",
        _ => method.ToString()
    };

    public static string Text(this DiscountType type) => type switch
    {
        DiscountType.Percentage => "Phần trăm",
        DiscountType.FixedAmount => "Số tiền cố định",
        _ => type.ToString()
    };

    public static string Text(this Gender gender) => gender switch
    {
        Gender.Male => "Nam",
        Gender.Female => "Nữ",
        _ => "Khác"
    };

    public static string Text(this MaintenanceStatus status) => status switch
    {
        MaintenanceStatus.Scheduled => "Đã lên lịch",
        MaintenanceStatus.InProgress => "Đang thực hiện",
        MaintenanceStatus.Completed => "Hoàn thành",
        MaintenanceStatus.Cancelled => "Đã hủy",
        _ => status.ToString()
    };

    public static string Text(this ContactStatus status) => status == ContactStatus.Processed ? "Đã xử lý" : "Mới";

    public static string Text(this SlotState state) => state switch
    {
        SlotState.Available => "Trống",
        SlotState.Booked => "Đã đặt",
        SlotState.Pending => "Chờ thanh toán",
        SlotState.Maintenance => "Bảo trì",
        _ => state.ToString()
    };

    public static string DayName(int dayOfWeek) => dayOfWeek switch
    {
        0 => "Chủ nhật",
        1 => "Thứ 2",
        2 => "Thứ 3",
        3 => "Thứ 4",
        4 => "Thứ 5",
        5 => "Thứ 6",
        6 => "Thứ 7",
        _ => "Không rõ"
    };

    public static string Hours(TimeOnly start, TimeOnly end) =>
        $"{start:HH:mm} – {end:HH:mm}";

    public static string DateText(DateOnly date) => date.ToString("dd/MM/yyyy");

    public static string DateTimeText(DateTime value) => value.ToString("dd/MM/yyyy HH:mm");
}

public class PagingModel
{
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalItems { get; set; }
    public int PageSize { get; set; } = AppConstants.PageSize;
}

public class ServiceResult<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }

    public static ServiceResult<T> Ok(T data, string message = "") =>
        new() { Success = true, Data = data, Message = message };

    public static ServiceResult<T> Fail(string message) =>
        new() { Success = false, Message = message };
}

public static class TextHelper
{
    public static string Slugify(string text)
    {
        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var chars = normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray();
        var plain = new string(chars).Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant();
        var builder = new System.Text.StringBuilder();
        var previousDash = false;
        foreach (var ch in plain)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                previousDash = false;
            }
            else if (!previousDash)
            {
                builder.Append('-');
                previousDash = true;
            }
        }
        return builder.ToString().Trim('-');
    }

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "MF";
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        }

        return string.Concat(parts[0][0], parts[^1][0]).ToUpperInvariant();
    }
}
