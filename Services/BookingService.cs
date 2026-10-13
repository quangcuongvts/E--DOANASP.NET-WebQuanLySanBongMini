using System.Data;
using Microsoft.AspNetCore.Identity;
using MiniFootballBooking.Data;

namespace MiniFootballBooking.Services;

public interface IBookingService
{
    Task<List<SlotDto>> GetSlotsAsync(int fieldId, DateOnly date);
    Task<List<FieldScheduleDto>> GetDayScheduleAsync(DateOnly date, int? fieldId);
    Task<QuoteResponse> QuoteAsync(QuoteRequest request, string? userId, bool isStaff);
    Task<ServiceResult<Booking>> CreateAsync(BookingCreateViewModel model, string actorId, bool isStaff);
    Task<ServiceResult<Booking>> CancelAsync(int bookingId, string actorId, string reason, bool isStaff);
    Task<ServiceResult<Booking>> RescheduleAsync(int bookingId, DateOnly date, TimeOnly start, TimeOnly end, string actorId, bool isStaff);
    Task<ServiceResult<Booking>> CheckInAsync(int bookingId, string staffId, string? note);
    Task<ServiceResult<Booking>> StartPlayingAsync(int bookingId, string staffId);
    Task<ServiceResult<Booking>> CheckOutAsync(int bookingId, string staffId, bool confirmPayment, PaymentMethod method);
    Task<ServiceResult<Booking>> AddServiceAsync(int bookingId, int serviceId, int quantity, string staffId);
    Task<ServiceResult<Booking>> UpdateStatusAsync(int bookingId, BookingStatus status, string actorId);
    Task<BookingDetailViewModel?> GetDetailAsync(int id, string? userId, bool isStaff);
}

public class BookingService : IBookingService
{
    private readonly ApplicationDbContext _db;
    private readonly IPricingService _pricing;
    private readonly IVoucherService _vouchers;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly IPaymentService _payments;
    private readonly SettingsReader _settings;
    private readonly UserManager<ApplicationUser> _users;

    public BookingService(
        ApplicationDbContext db,
        IPricingService pricing,
        IVoucherService vouchers,
        INotificationService notifications,
        IAuditService audit,
        IPaymentService payments,
        SettingsReader settings,
        UserManager<ApplicationUser> users)
    {
        _db = db;
        _pricing = pricing;
        _vouchers = vouchers;
        _notifications = notifications;
        _audit = audit;
        _payments = payments;
        _settings = settings;
        _users = users;
    }

    public async Task<List<SlotDto>> GetSlotsAsync(int fieldId, DateOnly date)
    {
        var schedule = await GetDayScheduleAsync(date, fieldId);
        return schedule.FirstOrDefault()?.Slots ?? [];
    }

    public async Task<List<FieldScheduleDto>> GetDayScheduleAsync(DateOnly date, int? fieldId)
    {
        var fields = await _db.FootballFields.AsNoTracking()
            .Where(f => f.IsActive && (fieldId == null || f.Id == fieldId))
            .OrderBy(f => f.FieldCode)
            .ToListAsync();
        var ids = fields.Select(f => f.Id).ToList();
        var rules = await _db.PriceRules.AsNoTracking().Where(r => ids.Contains(r.FieldId) && r.IsActive).ToListAsync();
        var bookings = await _db.Bookings.AsNoTracking()
            .Where(b => ids.Contains(b.FieldId) && b.BookingDate == date && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.NoShow)
            .ToListAsync();
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);
        var maintenances = await _db.Maintenances.AsNoTracking()
            .Where(m => ids.Contains(m.FieldId) && m.Status != MaintenanceStatus.Cancelled && m.StartDateTime < dayEnd && m.EndDateTime > dayStart)
            .ToListAsync();

        var open = await _settings.GetIntAsync("OpenHour", AppConstants.OpenHour);
        var close = await _settings.GetIntAsync("CloseHour", AppConstants.CloseHour);
        var result = new List<FieldScheduleDto>();

        foreach (var field in fields)
        {
            var fieldRules = rules.Where(r => r.FieldId == field.Id).ToList();
            var dto = new FieldScheduleDto { FieldId = field.Id, FieldName = field.Name, FieldCode = field.FieldCode };
            for (var hour = open; hour < close; hour++)
            {
                var start = new TimeOnly(hour, 0);
                var end = new TimeOnly(hour + 1, 0);
                var startDt = date.ToDateTime(start);
                var endDt = date.ToDateTime(end);
                var state = SlotState.Available;
                if (field.Status is FieldStatus.Maintenance or FieldStatus.Inactive
                    || maintenances.Any(m => m.FieldId == field.Id && m.StartDateTime < endDt && m.EndDateTime > startDt))
                {
                    state = SlotState.Maintenance;
                }
                else
                {
                    var hit = bookings.FirstOrDefault(b => b.FieldId == field.Id && b.StartTime < end && b.EndTime > start);
                    if (hit != null)
                    {
                        state = hit.Status == BookingStatus.Pending ? SlotState.Pending : SlotState.Booked;
                    }
                }

                var price = _pricing.Calculate(field, fieldRules, date, start, end).TotalPrice;
                dto.Slots.Add(new SlotDto
                {
                    Start = start.ToString("HH:mm"),
                    End = end.ToString("HH:mm"),
                    Status = state switch
                    {
                        SlotState.Booked => "booked",
                        SlotState.Pending => "pending",
                        SlotState.Maintenance => "maintenance",
                        _ => "available"
                    },
                    StatusText = state.Text(),
                    Price = price
                });
            }

            result.Add(dto);
        }

        return result;
    }

    public async Task<QuoteResponse> QuoteAsync(QuoteRequest request, string? userId, bool isStaff)
    {
        if (!TryParseRange(request.Date, request.Start, request.End, out var date, out var start, out var end, out var parseError))
        {
            return new QuoteResponse { Ok = false, Message = parseError };
        }

        var slotError = await ValidateSlotAsync(request.FieldId, date, start, end, null, isStaff);
        if (slotError != null)
        {
            return new QuoteResponse { Ok = false, Message = slotError };
        }

        var price = await _pricing.CalculateAsync(request.FieldId, date, start, end);
        var (serviceAmount, lines, serviceError) = await BuildServicesAsync(request.Services);
        if (serviceError != null)
        {
            return new QuoteResponse { Ok = false, Message = serviceError };
        }

        var subtotal = price.TotalPrice + serviceAmount;
        var voucher = await _vouchers.EvaluateAsync(request.VoucherCode, subtotal, userId);
        if (!string.IsNullOrWhiteSpace(request.VoucherCode) && !voucher.Ok)
        {
            return new QuoteResponse { Ok = false, Message = voucher.Message, FieldAmount = price.TotalPrice, ServiceAmount = serviceAmount };
        }

        return new QuoteResponse
        {
            Ok = true,
            Message = voucher.Message,
            Hours = price.TotalHours,
            FieldAmount = price.TotalPrice,
            ServiceAmount = serviceAmount,
            Discount = voucher.Discount,
            Total = Math.Max(0, subtotal - voucher.Discount),
            BasePrice = price.BasePrice,
            PeakHourCharge = price.PeakHourCharge,
            WeekendCharge = price.WeekendCharge,
            Lines = price.Lines,
            ServiceLines = lines
        };
    }

    public async Task<ServiceResult<Booking>> CreateAsync(BookingCreateViewModel model, string actorId, bool isStaff)
    {
        model.Services ??= [];
        var customerId = isStaff ? model.CustomerId : actorId;
        if (string.IsNullOrEmpty(customerId))
        {
            return ServiceResult<Booking>.Fail("Vui lòng chọn khách hàng.");
        }

        var customer = await _users.FindByIdAsync(customerId);
        if (customer == null)
        {
            return ServiceResult<Booking>.Fail("Không tìm thấy khách hàng.");
        }

        if (!customer.IsActive || (customer.LockoutEnd.HasValue && customer.LockoutEnd.Value > DateTimeOffset.UtcNow))
        {
            return ServiceResult<Booking>.Fail("Tài khoản khách hàng đang bị khóa, không thể đặt sân.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var slotError = await ValidateSlotAsync(model.FieldId, model.BookingDate, model.StartTime, model.EndTime, null, isStaff);
            if (slotError != null)
            {
                return ServiceResult<Booking>.Fail(slotError);
            }

            var price = await _pricing.CalculateAsync(model.FieldId, model.BookingDate, model.StartTime, model.EndTime);
            var (serviceAmount, _, serviceError) = await BuildServicesAsync(model.Services);
            if (serviceError != null)
            {
                return ServiceResult<Booking>.Fail(serviceError);
            }

            var subtotal = price.TotalPrice + serviceAmount;
            var voucher = await _vouchers.EvaluateAsync(model.VoucherCode, subtotal, customerId);
            if (!string.IsNullOrWhiteSpace(model.VoucherCode) && !voucher.Ok)
            {
                return ServiceResult<Booking>.Fail(voucher.Message);
            }

            var services = await LoadServicesAsync(model.Services);
            var discount = voucher.Discount;
            var total = Math.Max(0, subtotal - discount);
            var code = await NextBookingCodeAsync(AppClock.Today);
            var payAtField = model.PaymentMethod == PaymentMethod.Cash;
            var booking = new Booking
            {
                BookingCode = code,
                CustomerId = customerId,
                FieldId = model.FieldId,
                BookingDate = model.BookingDate,
                StartTime = model.StartTime,
                EndTime = model.EndTime,
                OriginalAmount = price.TotalPrice,
                ServiceAmount = serviceAmount,
                DiscountAmount = discount,
                TotalAmount = total,
                VoucherId = voucher.VoucherId,
                Status = payAtField ? BookingStatus.Confirmed : BookingStatus.Pending,
                PaymentStatus = PaymentStatus.Unpaid,
                Note = model.Note,
                CreatedAt = AppClock.Now,
                StaffId = isStaff ? actorId : null
            };

            foreach (var line in services)
            {
                booking.ServiceLines.Add(new BookingServiceLine
                {
                    ServiceId = line.Service.Id,
                    Quantity = line.Quantity,
                    UnitPrice = line.Service.Price,
                    TotalPrice = line.Service.Price * line.Quantity
                });
            }

            booking.Payments.Add(new Payment
            {
                PaymentCode = await NextPaymentCodeAsync(AppClock.Today),
                UserId = customerId,
                Amount = total,
                PaymentMethod = model.PaymentMethod,
                PaymentStatus = PaymentStatus.Unpaid,
                Note = payAtField ? "Thanh toán tại sân" : "Chờ thanh toán"
            });

            if (voucher.VoucherId != null)
            {
                var voucherEntity = await _db.Vouchers.FirstAsync(v => v.Id == voucher.VoucherId);
                voucherEntity.UsedCount += 1;
            }

            _db.Bookings.Add(booking);
            await _db.SaveChangesAsync();

            if (voucher.VoucherId != null)
            {
                _db.VoucherUsages.Add(new VoucherUsage
                {
                    VoucherId = voucher.VoucherId.Value,
                    UserId = customerId,
                    BookingId = booking.Id,
                    UsedAt = AppClock.Now
                });
                await _db.SaveChangesAsync();
            }

            var fieldName = await _db.FootballFields.Where(f => f.Id == booking.FieldId).Select(f => f.Name).FirstAsync();
            await _notifications.NotifyAsync(customerId, "Đặt sân thành công",
                $"Mã {booking.BookingCode} - {fieldName} ngày {booking.BookingDate:dd/MM/yyyy}.",
                NotificationType.BookingCreated, $"/Booking/Details/{booking.Id}");
            await _notifications.NotifyRolesAsync(["Admin", "Manager"], "Có booking mới",
                $"{booking.BookingCode} - {customer.FullName} - {fieldName}.",
                NotificationType.BookingCreated, $"/Admin/Bookings/Details/{booking.Id}");
            await _audit.LogAsync(actorId, "CREATE BOOKING", "Booking", booking.Id.ToString(), null, new
            {
                booking.BookingCode,
                booking.FieldId,
                booking.BookingDate,
                booking.StartTime,
                booking.EndTime,
                booking.TotalAmount
            });

            await tx.CommitAsync();
            return ServiceResult<Booking>.Ok(booking, payAtField
                ? "Đặt sân thành công. Vui lòng thanh toán tại sân."
                : "Giữ chỗ thành công. Vui lòng thanh toán để xác nhận.");
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync();
            return ServiceResult<Booking>.Fail("Khung giờ vừa được khách khác giữ. Vui lòng chọn giờ khác.");
        }
    }

    public async Task<ServiceResult<Booking>> CancelAsync(int bookingId, string actorId, string reason, bool isStaff)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return ServiceResult<Booking>.Fail("Vui lòng nhập lý do hủy.");
        }

        var booking = await _db.Bookings.Include(b => b.Payments).Include(b => b.Voucher).Include(b => b.Field)
            .FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking == null)
        {
            return ServiceResult<Booking>.Fail("Không tìm thấy đơn đặt sân.");
        }

        if (!isStaff && booking.CustomerId != actorId)
        {
            return ServiceResult<Booking>.Fail("Bạn không thể hủy đơn của người khác.");
        }

        if (booking.Status is not (BookingStatus.Pending or BookingStatus.Confirmed))
        {
            return ServiceResult<Booking>.Fail("Chỉ hủy được đơn chưa diễn ra.");
        }

        var start = booking.BookingDate.ToDateTime(booking.StartTime);
        if (start <= AppClock.Now)
        {
            return ServiceResult<Booking>.Fail("Đơn đã đến giờ hoặc đã qua, không thể hủy.");
        }

        var hours = (start - AppClock.Now).TotalHours;
        var fullHours = await _settings.GetIntAsync("CancelFullRefundHours", 24);
        var halfHours = await _settings.GetIntAsync("CancelHalfRefundHours", 6);
        var percent = hours >= fullHours ? 100 : hours >= halfHours ? 50 : 0;
        var paid = booking.Payments.Where(p => p.PaymentStatus == PaymentStatus.Paid).Sum(p => p.Amount);
        var oldStatus = booking.Status.ToString();

        booking.RefundPercent = paid > 0 ? percent : 0;
        booking.RefundAmount = Math.Round(paid * booking.RefundPercent / 100m, 0);
        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = AppClock.Now;
        booking.CancellationReason = reason.Trim();
        booking.CancelledById = actorId;
        booking.UpdatedAt = AppClock.Now;

        if (paid > 0 && percent > 0)
        {
            foreach (var payment in booking.Payments.Where(p => p.PaymentStatus == PaymentStatus.Paid))
            {
                payment.PaymentStatus = PaymentStatus.Refunded;
                payment.Note = $"Hoàn {percent}% ({booking.RefundAmount.VndText()}). {reason.Trim()}";
            }

            booking.PaymentStatus = PaymentStatus.Refunded;
        }

        if (booking.VoucherId != null)
        {
            var voucher = await _db.Vouchers.FirstAsync(v => v.Id == booking.VoucherId);
            voucher.UsedCount = Math.Max(0, voucher.UsedCount - 1);
            var usage = await _db.VoucherUsages.FirstOrDefaultAsync(u => u.BookingId == booking.Id);
            if (usage != null)
            {
                _db.VoucherUsages.Remove(usage);
            }
        }

        await _db.SaveChangesAsync();
        var refundText = booking.RefundPercent == 0
            ? "Không hoàn tiền theo chính sách hủy."
            : $"Hoàn {booking.RefundPercent}% tương đương {booking.RefundAmount.VndText()}.";
        await _notifications.NotifyAsync(booking.CustomerId, "Booking đã hủy",
            $"{booking.BookingCode} đã hủy. {refundText}", NotificationType.BookingCancelled, $"/Booking/Details/{booking.Id}");
        await _notifications.NotifyRolesAsync(["Admin", "Manager"], "Booking bị hủy",
            $"{booking.BookingCode} - {booking.Field?.Name}.", NotificationType.BookingCancelled, $"/Admin/Bookings/Details/{booking.Id}");
        await _audit.LogAsync(actorId, "CANCEL BOOKING", "Booking", booking.Id.ToString(),
            new { Status = oldStatus },
            new { Status = "Cancelled", booking.CancellationReason, booking.RefundPercent, booking.RefundAmount });

        return ServiceResult<Booking>.Ok(booking, $"Đã hủy {booking.BookingCode}. {refundText}");
    }

    public async Task<ServiceResult<Booking>> RescheduleAsync(int bookingId, DateOnly date, TimeOnly start, TimeOnly end, string actorId, bool isStaff)
    {
        var booking = await _db.Bookings.Include(b => b.ServiceLines).Include(b => b.Payments).Include(b => b.Voucher)
            .FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking == null)
        {
            return ServiceResult<Booking>.Fail("Không tìm thấy đơn đặt sân.");
        }

        if (!isStaff && booking.CustomerId != actorId)
        {
            return ServiceResult<Booking>.Fail("Bạn không thể đổi lịch đơn này.");
        }

        if (booking.Status is not (BookingStatus.Pending or BookingStatus.Confirmed))
        {
            return ServiceResult<Booking>.Fail("Chỉ đổi lịch đơn chưa diễn ra.");
        }

        var slotError = await ValidateSlotAsync(booking.FieldId, date, start, end, booking.Id, isStaff);
        if (slotError != null)
        {
            return ServiceResult<Booking>.Fail(slotError);
        }

        var price = await _pricing.CalculateAsync(booking.FieldId, date, start, end);
        var serviceAmount = booking.ServiceLines.Sum(s => s.TotalPrice);
        var subtotal = price.TotalPrice + serviceAmount;
        decimal discount = 0;
        if (booking.Voucher != null)
        {
            var check = await _vouchers.EvaluateAsync(booking.Voucher.VoucherCode, subtotal, null);
            if (!check.Ok && subtotal < booking.Voucher.MinOrderAmount)
            {
                return ServiceResult<Booking>.Fail("Lịch mới không còn đủ điều kiện voucher. Hãy hủy voucher trước khi đổi giờ.");
            }

            discount = booking.Voucher.DiscountType == DiscountType.Percentage
                ? Math.Min(booking.Voucher.MaxDiscountAmount ?? decimal.MaxValue, Math.Round(subtotal * booking.Voucher.DiscountValue / 100m, 0))
                : booking.Voucher.DiscountValue;
            discount = Math.Min(discount, subtotal);
        }

        var oldValue = new { booking.BookingDate, booking.StartTime, booking.EndTime, booking.TotalAmount };
        booking.BookingDate = date;
        booking.StartTime = start;
        booking.EndTime = end;
        booking.OriginalAmount = price.TotalPrice;
        booking.ServiceAmount = serviceAmount;
        booking.DiscountAmount = discount;
        booking.TotalAmount = Math.Max(0, subtotal - discount);
        booking.UpdatedAt = AppClock.Now;
        SyncPaymentStatus(booking);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(actorId, "UPDATE BOOKING", "Booking", booking.Id.ToString(), oldValue, new
        {
            booking.BookingDate,
            booking.StartTime,
            booking.EndTime,
            booking.TotalAmount
        });
        await _notifications.NotifyAsync(booking.CustomerId, "Lịch đặt sân đã đổi",
            $"{booking.BookingCode} chuyển sang {date:dd/MM/yyyy} {DisplayNames.Hours(start, end)}.",
            NotificationType.BookingConfirmed, $"/Booking/Details/{booking.Id}");
        return ServiceResult<Booking>.Ok(booking, "Đổi lịch thành công.");
    }

    public async Task<ServiceResult<Booking>> CheckInAsync(int bookingId, string staffId, string? note)
    {
        var booking = await _db.Bookings.Include(b => b.Field).FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking == null)
        {
            return ServiceResult<Booking>.Fail("Không tìm thấy đơn đặt sân.");
        }

        if (booking.Status != BookingStatus.Confirmed)
        {
            return ServiceResult<Booking>.Fail("Chỉ check-in đơn đã xác nhận.");
        }

        if (booking.BookingDate != AppClock.Today)
        {
            return ServiceResult<Booking>.Fail("Chỉ check-in trong ngày thi đấu.");
        }

        booking.Status = BookingStatus.CheckedIn;
        booking.CheckInTime = AppClock.Now;
        booking.CheckInNote = note;
        booking.StaffId = staffId;
        booking.UpdatedAt = AppClock.Now;
        if (booking.Field != null)
        {
            booking.Field.Status = FieldStatus.Occupied;
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync(staffId, "UPDATE BOOKING", "Booking", booking.Id.ToString(), new { Status = "Confirmed" }, new { Status = "CheckedIn", Note = note });
        return ServiceResult<Booking>.Ok(booking, $"Đã check-in {booking.BookingCode}.");
    }

    public async Task<ServiceResult<Booking>> StartPlayingAsync(int bookingId, string staffId)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking == null || booking.Status != BookingStatus.CheckedIn)
        {
            return ServiceResult<Booking>.Fail("Cần check-in trước khi bắt đầu trận.");
        }

        booking.Status = BookingStatus.Playing;
        booking.StaffId = staffId;
        booking.UpdatedAt = AppClock.Now;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(staffId, "UPDATE BOOKING", "Booking", booking.Id.ToString(), new { Status = "CheckedIn" }, new { Status = "Playing" });
        return ServiceResult<Booking>.Ok(booking, "Đã bắt đầu trận đấu.");
    }

    public async Task<ServiceResult<Booking>> CheckOutAsync(int bookingId, string staffId, bool confirmPayment, PaymentMethod method)
    {
        var booking = await _db.Bookings.Include(b => b.Field).FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking == null)
        {
            return ServiceResult<Booking>.Fail("Không tìm thấy đơn đặt sân.");
        }

        if (booking.Status is not (BookingStatus.CheckedIn or BookingStatus.Playing))
        {
            return ServiceResult<Booking>.Fail("Chỉ check-out khi khách đang check-in hoặc đang chơi.");
        }

        if (booking.PaymentStatus is not (PaymentStatus.Paid or PaymentStatus.Refunded))
        {
            if (!confirmPayment)
            {
                return ServiceResult<Booking>.Fail("Cần xác nhận thanh toán trước khi hoàn thành.");
            }

            var pay = await _payments.ConfirmAsync(bookingId, staffId, method, true, "Thu tiền khi check-out");
            if (!pay.Success)
            {
                return ServiceResult<Booking>.Fail(pay.Message);
            }

            await _db.Entry(booking).ReloadAsync();
        }

        booking.Status = BookingStatus.Completed;
        booking.CheckOutTime = AppClock.Now;
        booking.StaffId = staffId;
        booking.UpdatedAt = AppClock.Now;
        if (booking.Field != null && booking.Field.Status == FieldStatus.Occupied)
        {
            var stillBusy = await _db.Bookings.AnyAsync(b =>
                b.Id != booking.Id && b.FieldId == booking.FieldId && b.BookingDate == AppClock.Today
                && b.Status == BookingStatus.Playing);
            if (!stillBusy)
            {
                booking.Field.Status = FieldStatus.Available;
            }
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync(staffId, "UPDATE BOOKING", "Booking", booking.Id.ToString(), null, new { Status = "Completed", booking.CheckOutTime, booking.TotalAmount });
        return ServiceResult<Booking>.Ok(booking, $"Đã check-out và hoàn thành {booking.BookingCode}.");
    }

    public async Task<ServiceResult<Booking>> AddServiceAsync(int bookingId, int serviceId, int quantity, string staffId)
    {
        if (quantity < 1)
        {
            return ServiceResult<Booking>.Fail("Số lượng không hợp lệ.");
        }

        var booking = await _db.Bookings.Include(b => b.ServiceLines).Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking == null)
        {
            return ServiceResult<Booking>.Fail("Không tìm thấy đơn đặt sân.");
        }

        if (booking.Status is BookingStatus.Cancelled or BookingStatus.Completed or BookingStatus.NoShow)
        {
            return ServiceResult<Booking>.Fail("Không thể thêm dịch vụ cho đơn này.");
        }

        var service = await _db.ServiceItems.FirstOrDefaultAsync(s => s.Id == serviceId && s.IsActive);
        if (service == null)
        {
            return ServiceResult<Booking>.Fail("Dịch vụ không khả dụng.");
        }

        var line = booking.ServiceLines.FirstOrDefault(s => s.ServiceId == serviceId);
        if (line == null)
        {
            line = new BookingServiceLine { BookingId = booking.Id, ServiceId = serviceId, UnitPrice = service.Price };
            booking.ServiceLines.Add(line);
        }

        line.Quantity += quantity;
        line.UnitPrice = service.Price;
        line.TotalPrice = line.UnitPrice * line.Quantity;
        booking.ServiceAmount = booking.ServiceLines.Sum(s => s.TotalPrice);
        booking.TotalAmount = Math.Max(0, booking.OriginalAmount + booking.ServiceAmount - booking.DiscountAmount);
        booking.UpdatedAt = AppClock.Now;
        SyncPaymentStatus(booking);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(staffId, "UPDATE BOOKING", "Booking", booking.Id.ToString(), null, new { Service = service.Name, quantity, booking.TotalAmount });
        return ServiceResult<Booking>.Ok(booking, $"Đã thêm {service.Name}.");
    }

    public async Task<ServiceResult<Booking>> UpdateStatusAsync(int bookingId, BookingStatus status, string actorId)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking == null)
        {
            return ServiceResult<Booking>.Fail("Không tìm thấy đơn đặt sân.");
        }

        var allowed = booking.Status switch
        {
            BookingStatus.Pending => new[] { BookingStatus.Confirmed, BookingStatus.Cancelled },
            BookingStatus.Confirmed => new[] { BookingStatus.CheckedIn, BookingStatus.Cancelled, BookingStatus.NoShow },
            BookingStatus.CheckedIn => new[] { BookingStatus.Playing, BookingStatus.Completed, BookingStatus.NoShow },
            BookingStatus.Playing => new[] { BookingStatus.Completed },
            _ => Array.Empty<BookingStatus>()
        };

        if (!allowed.Contains(status))
        {
            return ServiceResult<Booking>.Fail("Không thể chuyển trạng thái này.");
        }

        if (status == BookingStatus.Cancelled)
        {
            return await CancelAsync(bookingId, actorId, "Nhân viên hủy đơn", true);
        }

        if (status == BookingStatus.CheckedIn)
        {
            return await CheckInAsync(bookingId, actorId, null);
        }

        if (status == BookingStatus.Playing)
        {
            return await StartPlayingAsync(bookingId, actorId);
        }

        if (status == BookingStatus.Completed)
        {
            return await CheckOutAsync(bookingId, actorId, booking.PaymentStatus == PaymentStatus.Paid, PaymentMethod.Cash);
        }

        if (status == BookingStatus.NoShow && booking.BookingDate.ToDateTime(booking.StartTime) > AppClock.Now)
        {
            return ServiceResult<Booking>.Fail("Chưa đến giờ đá, chưa thể đánh dấu không đến.");
        }

        var old = booking.Status.ToString();
        booking.Status = status;
        booking.UpdatedAt = AppClock.Now;
        if (status == BookingStatus.Confirmed)
        {
            await _notifications.NotifyAsync(booking.CustomerId, "Booking đã xác nhận",
                $"Đơn {booking.BookingCode} đã được xác nhận.", NotificationType.BookingConfirmed, $"/Booking/Details/{booking.Id}");
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync(actorId, "UPDATE BOOKING", "Booking", booking.Id.ToString(), new { Status = old }, new { Status = status.ToString() });
        return ServiceResult<Booking>.Ok(booking, "Đã cập nhật trạng thái.");
    }

    public async Task<BookingDetailViewModel?> GetDetailAsync(int id, string? userId, bool isStaff)
    {
        var booking = await _db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Field)
            .Include(b => b.Voucher)
            .Include(b => b.Staff)
            .Include(b => b.CancelledBy)
            .Include(b => b.Payments)
            .Include(b => b.ServiceLines).ThenInclude(s => s.Service)
            .Include(b => b.Reviews).ThenInclude(r => r.Customer)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null)
        {
            return null;
        }

        if (!isStaff && booking.CustomerId != userId)
        {
            return null;
        }

        var review = booking.Reviews.FirstOrDefault();
        var start = booking.BookingDate.ToDateTime(booking.StartTime);
        return new BookingDetailViewModel
        {
            Id = booking.Id,
            BookingCode = booking.BookingCode,
            CustomerName = booking.Customer?.FullName ?? "",
            Phone = booking.Customer?.PhoneNumber ?? "",
            Email = booking.Customer?.Email ?? "",
            FieldName = booking.Field?.Name ?? "",
            FieldCode = booking.Field?.FieldCode ?? "",
            FieldId = booking.FieldId,
            Area = booking.Field?.Area ?? "",
            BookingDate = booking.BookingDate,
            StartTime = booking.StartTime,
            EndTime = booking.EndTime,
            OriginalAmount = booking.OriginalAmount,
            ServiceAmount = booking.ServiceAmount,
            DiscountAmount = booking.DiscountAmount,
            TotalAmount = booking.TotalAmount,
            RefundAmount = booking.RefundAmount,
            RefundPercent = booking.RefundPercent,
            VoucherCode = booking.Voucher?.VoucherCode,
            Status = booking.Status,
            PaymentStatus = booking.PaymentStatus,
            Note = booking.Note,
            CancellationReason = booking.CancellationReason,
            CancelledByName = booking.CancelledBy?.FullName,
            CancelledAt = booking.CancelledAt,
            CheckInTime = booking.CheckInTime,
            CheckOutTime = booking.CheckOutTime,
            CheckInNote = booking.CheckInNote,
            StaffName = booking.Staff?.FullName,
            CreatedAt = booking.CreatedAt,
            Services = booking.ServiceLines.Select(s => new ServiceQuoteLine
            {
                Name = s.Service?.Name ?? "Dịch vụ",
                Quantity = s.Quantity,
                Total = s.TotalPrice
            }).ToList(),
            Payments = booking.Payments.OrderByDescending(p => p.Id).ToList(),
            CanCancel = booking.Status is BookingStatus.Pending or BookingStatus.Confirmed && start > AppClock.Now,
            CanReschedule = booking.Status is BookingStatus.Pending or BookingStatus.Confirmed && start > AppClock.Now,
            CanReview = booking.Status == BookingStatus.Completed && review == null && booking.CustomerId == userId,
            CanPay = booking.PaymentStatus is PaymentStatus.Unpaid or PaymentStatus.Failed or PaymentStatus.PartiallyPaid
                     && booking.Status is not (BookingStatus.Cancelled or BookingStatus.NoShow),
            IsStaffView = isStaff,
            Review = review == null ? null : new ReviewCardViewModel
            {
                Id = review.Id,
                CustomerName = review.Customer?.FullName ?? "",
                Rating = review.Rating,
                Comment = review.Comment,
                CreatedAt = review.CreatedAt,
                IsVisible = review.IsVisible,
                BookingId = booking.Id,
                CanEdit = review.CustomerId == userId
            }
        };
    }

    /// <summary>
    /// Trùng lịch khi khoảng mới cắt khoảng đã giữ:
    /// newStart &lt; existingEnd và newEnd &gt; existingStart.
    /// Bỏ qua đơn đã hủy hoặc không đến.
    /// </summary>
    private async Task<string?> ValidateSlotAsync(int fieldId, DateOnly date, TimeOnly start, TimeOnly end, int? ignoreBookingId, bool isStaff)
    {
        if (end <= start)
        {
            return "Giờ kết thúc phải lớn hơn giờ bắt đầu.";
        }

        if (start.Minute != 0 || end.Minute != 0)
        {
            return "Chỉ đặt theo khung giờ tròn.";
        }

        var open = await _settings.GetIntAsync("OpenHour", AppConstants.OpenHour);
        var close = await _settings.GetIntAsync("CloseHour", AppConstants.CloseHour);
        if (start.Hour < open || end.Hour > close || (end.Hour == close && end.Minute > 0))
        {
            return $"Sân phục vụ từ {open:00}:00 đến {close:00}:00.";
        }

        var today = AppClock.Today;
        var now = TimeOnly.FromDateTime(AppClock.Now);
        if (date < today)
        {
            return "Không được đặt ngày trong quá khứ.";
        }

        if (date == today && end <= now)
        {
            return "Khung giờ đã kết thúc.";
        }

        if (date == today && start < now && !isStaff)
        {
            return "Không được đặt giờ đã qua.";
        }

        var field = await _db.FootballFields.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fieldId);
        if (field == null || !field.IsActive || field.Status == FieldStatus.Inactive)
        {
            return "Sân không tồn tại hoặc đã ngừng hoạt động.";
        }

        if (field.Status == FieldStatus.Maintenance)
        {
            return "Sân đang bảo trì, không thể đặt.";
        }

        var startDt = date.ToDateTime(start);
        var endDt = date.ToDateTime(end);
        var maintained = await _db.Maintenances.AnyAsync(m =>
            m.FieldId == fieldId &&
            m.Status != MaintenanceStatus.Cancelled &&
            m.StartDateTime < endDt &&
            m.EndDateTime > startDt);
        if (maintained)
        {
            return "Sân có lịch bảo trì trong khung giờ này.";
        }

        var overlap = await _db.Bookings.AnyAsync(b =>
            b.FieldId == fieldId &&
            b.BookingDate == date &&
            (ignoreBookingId == null || b.Id != ignoreBookingId) &&
            b.Status != BookingStatus.Cancelled &&
            b.Status != BookingStatus.NoShow &&
            b.StartTime < end &&
            b.EndTime > start);

        return overlap ? "Khung giờ đã có người đặt. Người xác nhận trước được giữ lịch." : null;
    }

    private async Task<(decimal Amount, List<ServiceQuoteLine> Lines, string? Error)> BuildServicesAsync(List<ServiceLineInput> inputs)
    {
        var selected = (inputs ?? []).Where(s => s.ServiceId > 0 && s.Quantity > 0).ToList();
        if (selected.Count == 0)
        {
            return (0, [], null);
        }

        var loaded = await LoadServicesAsync(selected);
        if (loaded.Count != selected.Select(s => s.ServiceId).Distinct().Count())
        {
            return (0, [], "Có dịch vụ không còn kinh doanh.");
        }

        var lines = loaded.Select(x => new ServiceQuoteLine
        {
            Name = x.Service.Name,
            Quantity = x.Quantity,
            Total = x.Service.Price * x.Quantity
        }).ToList();
        return (lines.Sum(l => l.Total), lines, null);
    }

    private async Task<List<(ServiceItem Service, int Quantity)>> LoadServicesAsync(List<ServiceLineInput> inputs)
    {
        var selected = (inputs ?? []).Where(s => s.ServiceId > 0 && s.Quantity > 0)
            .GroupBy(s => s.ServiceId)
            .Select(g => new { Id = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();
        var ids = selected.Select(s => s.Id).ToList();
        var services = await _db.ServiceItems.Where(s => ids.Contains(s.Id) && s.IsActive).ToListAsync();
        return selected
            .Select(s => (Service: services.FirstOrDefault(x => x.Id == s.Id), s.Quantity))
            .Where(x => x.Service != null)
            .Select(x => (x.Service!, x.Quantity))
            .ToList();
    }

    private async Task<string> NextBookingCodeAsync(DateOnly date)
    {
        var prefix = "BK" + date.ToString("yyyyMMdd");
        var last = await _db.Bookings.Where(b => b.BookingCode.StartsWith(prefix))
            .OrderByDescending(b => b.BookingCode)
            .Select(b => b.BookingCode)
            .FirstOrDefaultAsync();
        var seq = last != null && int.TryParse(last[^4..], out var number) ? number + 1 : 1;
        return prefix + seq.ToString("D4");
    }

    private async Task<string> NextPaymentCodeAsync(DateOnly date)
    {
        var prefix = "PM" + date.ToString("yyyyMMdd");
        var last = await _db.Payments.Where(p => p.PaymentCode.StartsWith(prefix))
            .OrderByDescending(p => p.PaymentCode)
            .Select(p => p.PaymentCode)
            .FirstOrDefaultAsync();
        var seq = last != null && int.TryParse(last[^4..], out var number) ? number + 1 : 1;
        return prefix + seq.ToString("D4");
    }

    private static bool TryParseRange(string dateText, string startText, string endText, out DateOnly date, out TimeOnly start, out TimeOnly end, out string error)
    {
        error = string.Empty;
        date = default;
        start = default;
        end = default;
        if (!DateOnly.TryParse(dateText, out date) || !TimeOnly.TryParse(startText, out start) || !TimeOnly.TryParse(endText, out end))
        {
            error = "Ngày hoặc giờ không hợp lệ.";
            return false;
        }

        return true;
    }

    private static void SyncPaymentStatus(Booking booking)
    {
        var paid = booking.Payments.Where(p => p.PaymentStatus == PaymentStatus.Paid).Sum(p => p.Amount);
        if (paid <= 0)
        {
            if (booking.PaymentStatus != PaymentStatus.Failed)
            {
                booking.PaymentStatus = PaymentStatus.Unpaid;
            }

            return;
        }

        booking.PaymentStatus = paid >= booking.TotalAmount ? PaymentStatus.Paid : PaymentStatus.PartiallyPaid;
    }
}
