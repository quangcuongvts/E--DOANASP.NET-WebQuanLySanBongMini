using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MiniFootballBooking.Services;

namespace MiniFootballBooking.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Manager,Staff")]
public class BookingsController : Controller
{
    private readonly Data.ApplicationDbContext _db;
    private readonly IBookingService _booking;
    private readonly IPaymentService _payments;
    private readonly UserManager<ApplicationUser> _users;

    public BookingsController(Data.ApplicationDbContext db, IBookingService booking, IPaymentService payments, UserManager<ApplicationUser> users)
    {
        _db = db;
        _booking = booking;
        _payments = payments;
        _users = users;
    }

    public async Task<IActionResult> Index(string? q, BookingStatus? status, PaymentStatus? payment, int? fieldId, DateOnly? from, DateOnly? to, int page = 1)
    {
        var query = _db.Bookings.Include(b => b.Field).Include(b => b.Customer).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim();
            query = query.Where(b => b.BookingCode.Contains(keyword) || b.Customer!.FullName.Contains(keyword) || (b.Customer.PhoneNumber != null && b.Customer.PhoneNumber.Contains(keyword)));
        }

        if (status != null)
        {
            query = query.Where(b => b.Status == status);
        }

        if (payment != null)
        {
            query = query.Where(b => b.PaymentStatus == payment);
        }

        if (fieldId != null)
        {
            query = query.Where(b => b.FieldId == fieldId);
        }

        if (from != null)
        {
            query = query.Where(b => b.BookingDate >= from);
        }

        if (to != null)
        {
            query = query.Where(b => b.BookingDate <= to);
        }

        page = page < 1 ? 1 : page;
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(b => b.BookingDate).ThenBy(b => b.StartTime)
            .Skip((page - 1) * AppConstants.PageSize).Take(AppConstants.PageSize)
            .Select(b => new BookingListItemViewModel
            {
                Id = b.Id,
                BookingCode = b.BookingCode,
                FieldName = b.Field!.Name,
                CustomerName = b.Customer!.FullName,
                Phone = b.Customer.PhoneNumber ?? "",
                BookingDate = b.BookingDate,
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                TotalAmount = b.TotalAmount,
                Status = b.Status,
                PaymentStatus = b.PaymentStatus,
                CreatedAt = b.CreatedAt
            }).ToListAsync();

        return View(new BookingListViewModel
        {
            Items = items,
            Keyword = q,
            Status = status,
            PaymentStatus = payment,
            FieldId = fieldId,
            From = from,
            To = to,
            Fields = await _db.FootballFields.OrderBy(f => f.FieldCode).ToListAsync(),
            Paging = new PagingModel { Page = page, TotalItems = total, TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)AppConstants.PageSize)) }
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var user = await _users.GetUserAsync(User);
        var detail = await _booking.GetDetailAsync(id, user?.Id, true);
        if (detail == null)
        {
            return NotFound();
        }

        ViewBag.Services = await _db.ServiceItems.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
        return View(detail);
    }

    public async Task<IActionResult> Create(int? fieldId, DateOnly? date)
    {
        ViewBag.Page = await Page(fieldId, date ?? AppClock.Today);
        return View(new BookingCreateViewModel { FieldId = fieldId ?? 0, BookingDate = date ?? AppClock.Today, PaymentMethod = PaymentMethod.Cash });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BookingCreateViewModel model)
    {
        var actor = await _users.GetUserAsync(User);
        if (string.IsNullOrEmpty(model.CustomerId))
        {
            if (string.IsNullOrWhiteSpace(model.WalkInName) || string.IsNullOrWhiteSpace(model.WalkInPhone))
            {
                TempData["Error"] = "Nhập khách hàng có sẵn hoặc tên và số điện thoại khách vãng lai.";
                ViewBag.Page = await Page(model.FieldId, model.BookingDate);
                return View(model);
            }

            var matches = await _users.Users.Where(u => u.PhoneNumber == model.WalkInPhone).ToListAsync();
            ApplicationUser? customer = null;
            foreach (var match in matches)
            {
                if (await _users.IsInRoleAsync(match, "Customer"))
                {
                    customer = match;
                    break;
                }
            }

            if (customer == null)
            {
                customer = new ApplicationUser
                {
                    UserName = $"walkin{model.WalkInPhone}@football.local",
                    Email = $"walkin{model.WalkInPhone}@football.local",
                    EmailConfirmed = true,
                    FullName = model.WalkInName.Trim(),
                    PhoneNumber = model.WalkInPhone,
                    IsActive = true,
                    LockoutEnabled = true,
                    CreatedAt = AppClock.Now
                };
                var created = await _users.CreateAsync(customer, "Walkin@123");
                if (!created.Succeeded)
                {
                    TempData["Error"] = string.Join(" ", created.Errors.Select(e => e.Description));
                    ViewBag.Page = await Page(model.FieldId, model.BookingDate);
                    return View(model);
                }

                await _users.AddToRoleAsync(customer, "Customer");
                TempData["Success"] = "Đã tạo khách vãng lai. Mật khẩu tạm: Walkin@123";
            }

            model.CustomerId = customer.Id;
        }

        var result = await _booking.CreateAsync(model, actor!.Id, true);
        if (!result.Success || result.Data == null)
        {
            TempData["Error"] = result.Message;
            ViewBag.Page = await Page(model.FieldId, model.BookingDate);
            return View(model);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Details), new { id = result.Data.Id });
    }

    [HttpGet]
    public async Task<IActionResult> LookupCustomer(string phone)
    {
        var matches = await _users.Users.Where(u => u.PhoneNumber == phone).ToListAsync();
        foreach (var match in matches)
        {
            if (await _users.IsInRoleAsync(match, "Customer"))
            {
                return Json(new CustomerLookupViewModel { Found = true, Id = match.Id, FullName = match.FullName, Phone = match.PhoneNumber, Email = match.Email });
            }
        }

        return Json(new CustomerLookupViewModel { Found = false });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn(CheckInViewModel model)
    {
        var user = await _users.GetUserAsync(User);
        var result = await _booking.CheckInAsync(model.Id, user!.Id, model.Note);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int id)
    {
        var user = await _users.GetUserAsync(User);
        var result = await _booking.StartPlayingAsync(id, user!.Id);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckOut(int id, bool confirmPayment = true, PaymentMethod method = PaymentMethod.Cash)
    {
        var user = await _users.GetUserAsync(User);
        var result = await _booking.CheckOutAsync(id, user!.Id, confirmPayment, method);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddService(AddServiceViewModel model)
    {
        var user = await _users.GetUserAsync(User);
        var result = await _booking.AddServiceAsync(model.BookingId, model.ServiceId, model.Quantity, user!.Id);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id = model.BookingId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(CancelBookingViewModel model)
    {
        var user = await _users.GetUserAsync(User);
        var result = await _booking.CancelAsync(model.Id, user!.Id, model.Reason, true);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, BookingStatus status)
    {
        var user = await _users.GetUserAsync(User);
        var result = await _booking.UpdateStatusAsync(id, status, user!.Id);
        if (Request.Headers.Accept.ToString().Contains("application/json"))
        {
            return Json(new { ok = result.Success, message = result.Message });
        }

        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPayment(int id, PaymentMethod method)
    {
        var user = await _users.GetUserAsync(User);
        var result = await _payments.ConfirmAsync(id, user!.Id, method, true, "Nhân viên xác nhận thanh toán");
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<BookingPageViewModel> Page(int? fieldId, DateOnly date)
    {
        return new BookingPageViewModel
        {
            FieldId = fieldId,
            Date = date,
            IsCounter = true,
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
