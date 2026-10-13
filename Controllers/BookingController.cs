using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using MiniFootballBooking.Services;

namespace MiniFootballBooking.Controllers;

public class BookingController : Controller
{
    private readonly IBookingService _booking;
    private readonly IPaymentService _payments;
    private readonly Data.ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SettingsReader _settings;

    public BookingController(IBookingService booking, IPaymentService payments, Data.ApplicationDbContext db, UserManager<ApplicationUser> users, SettingsReader settings)
    {
        _booking = booking;
        _payments = payments;
        _db = db;
        _users = users;
        _settings = settings;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Schedule(int? fieldId, DateOnly? date)
    {
        var selected = date ?? AppClock.Today;
        ViewBag.Fields = await _db.FootballFields.Where(f => f.IsActive).OrderBy(f => f.FieldCode).ToListAsync();
        ViewBag.Date = selected;
        ViewBag.FieldId = fieldId;
        var schedules = await _booking.GetDayScheduleAsync(selected, fieldId);
        return View(schedules);
    }

    [AllowAnonymous]
    public async Task<IActionResult> Slots(int fieldId, DateOnly date)
    {
        var slots = await _booking.GetSlotsAsync(fieldId, date);
        return Json(new { slots });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Quote([FromBody] QuoteRequest request)
    {
        var user = await _users.GetUserAsync(User);
        var quote = await _booking.QuoteAsync(request, user?.Id, IsStaffUser);
        return Json(quote);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckVoucher([FromBody] VoucherCheckRequest request)
    {
        var user = await _users.GetUserAsync(User);
        var result = await HttpContext.RequestServices.GetRequiredService<IVoucherService>().EvaluateAsync(request.Code, request.OrderAmount, user?.Id);
        return Json(result);
    }

    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Create(int? fieldId, DateOnly? date, string? start, string? end)
    {
        return View(await BuildPage(fieldId, date ?? AppClock.Today, start, end, false));
    }

    [Authorize(Roles = "Customer")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BookingCreateViewModel model)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vui lòng chọn đủ sân, ngày và giờ.";
            return View(await BuildPage(model.FieldId, model.BookingDate, model.StartTime.ToString("HH:mm"), model.EndTime.ToString("HH:mm"), false));
        }

        var result = await _booking.CreateAsync(model, user.Id, false);
        if (!result.Success || result.Data == null)
        {
            TempData["Error"] = result.Message;
            return View(await BuildPage(model.FieldId, model.BookingDate, model.StartTime.ToString("HH:mm"), model.EndTime.ToString("HH:mm"), false));
        }

        TempData["Success"] = result.Message;
        return model.PaymentMethod == PaymentMethod.Cash
            ? RedirectToAction(nameof(Details), new { id = result.Data.Id })
            : RedirectToAction(nameof(Payment), new { id = result.Data.Id });
    }

    [Authorize]
    public async Task<IActionResult> Mine(string filter = "all", int page = 1)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        if (!await _users.IsInRoleAsync(user, "Customer"))
        {
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }

        var today = AppClock.Today;
        var query = _db.Bookings.Include(b => b.Field).Where(b => b.CustomerId == user.Id);
        query = filter switch
        {
            "upcoming" => query.Where(b => b.BookingDate >= today && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.Completed && b.Status != BookingStatus.NoShow),
            "completed" => query.Where(b => b.Status == BookingStatus.Completed),
            "cancelled" => query.Where(b => b.Status == BookingStatus.Cancelled || b.Status == BookingStatus.NoShow),
            _ => query
        };
        page = page < 1 ? 1 : page;
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(b => b.BookingDate).ThenByDescending(b => b.StartTime)
            .Skip((page - 1) * AppConstants.PageSize).Take(AppConstants.PageSize)
            .Select(b => new BookingListItemViewModel
            {
                Id = b.Id,
                BookingCode = b.BookingCode,
                FieldName = b.Field!.Name,
                BookingDate = b.BookingDate,
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                TotalAmount = b.TotalAmount,
                Status = b.Status,
                PaymentStatus = b.PaymentStatus
            }).ToListAsync();
        return View(new BookingListViewModel
        {
            Items = items,
            Filter = filter,
            Paging = new PagingModel
            {
                Page = page,
                TotalItems = total,
                TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)AppConstants.PageSize))
            }
        });
    }

    [Authorize]
    public async Task<IActionResult> Details(int id)
    {
        var user = await _users.GetUserAsync(User);
        var detail = await _booking.GetDetailAsync(id, user?.Id, IsStaffUser);
        if (detail == null)
        {
            return await _db.Bookings.AnyAsync(b => b.Id == id) ? Forbid() : NotFound();
        }

        ViewBag.Services = await _db.ServiceItems.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
        return View(detail);
    }

    [Authorize]
    public async Task<IActionResult> Payment(int id)
    {
        var user = await _users.GetUserAsync(User);
        var detail = await _booking.GetDetailAsync(id, user?.Id, IsStaffUser);
        if (detail == null)
        {
            return NotFound();
        }

        ViewBag.Settings = await _settings.AllAsync();
        return View(detail);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int id, PaymentMethod method, bool success = true)
    {
        var user = await _users.GetUserAsync(User);
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == id);
        if (booking == null)
        {
            return NotFound();
        }

        if (!IsStaffUser && booking.CustomerId != user?.Id)
        {
            return Forbid();
        }

        var result = await _payments.ConfirmAsync(id, user!.Id, method, success, success ? "Khách xác nhận thanh toán" : "Thanh toán demo thất bại");
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(CancelBookingViewModel model)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var result = await _booking.CancelAsync(model.Id, user.Id, model.Reason, IsStaffUser);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [Authorize]
    public async Task<IActionResult> Reschedule(int id)
    {
        var user = await _users.GetUserAsync(User);
        var detail = await _booking.GetDetailAsync(id, user?.Id, IsStaffUser);
        if (detail == null || !detail.CanReschedule)
        {
            TempData["Error"] = "Đơn này không thể đổi lịch.";
            return RedirectToAction(nameof(Details), new { id });
        }

        ViewBag.Page = await BuildPage(detail.FieldId, detail.BookingDate, detail.StartTime.ToString("HH:mm"), detail.EndTime.ToString("HH:mm"), false);
        return View(new RescheduleViewModel
        {
            Id = detail.Id,
            BookingCode = detail.BookingCode,
            FieldId = detail.FieldId,
            FieldName = detail.FieldName,
            BookingDate = detail.BookingDate,
            StartTime = detail.StartTime,
            EndTime = detail.EndTime
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(RescheduleViewModel model)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var result = await _booking.RescheduleAsync(model.Id, model.BookingDate, model.StartTime, model.EndTime, user.Id, IsStaffUser);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [Authorize]
    public async Task<IActionResult> Invoice(int id)
    {
        var user = await _users.GetUserAsync(User);
        var detail = await _booking.GetDetailAsync(id, user?.Id, IsStaffUser);
        if (detail == null)
        {
            return NotFound();
        }

        ViewBag.Settings = await _settings.AllAsync();
        return View(detail);
    }

    private bool IsStaffUser => User.IsInRole("Admin") || User.IsInRole("Manager") || User.IsInRole("Staff");

    private async Task<BookingPageViewModel> BuildPage(int? fieldId, DateOnly date, string? start, string? end, bool counter)
    {
        return new BookingPageViewModel
        {
            FieldId = fieldId,
            Date = date < AppClock.Today ? AppClock.Today : date,
            Start = start,
            End = end,
            IsCounter = counter,
            Fields = await _db.FootballFields.Where(f => f.IsActive && f.Status != FieldStatus.Inactive)
                .OrderBy(f => f.FieldCode)
                .Select(f => new FieldOptionViewModel
                {
                    Id = f.Id,
                    Name = f.Name,
                    Code = f.FieldCode,
                    TypeName = f.FieldType!.Name,
                    BasePrice = f.BasePrice,
                    ImageUrl = f.ImageUrl,
                    Area = f.Area
                }).ToListAsync(),
            Services = await _db.ServiceItems.Where(s => s.IsActive).OrderBy(s => s.Price).ToListAsync()
        };
    }
}
