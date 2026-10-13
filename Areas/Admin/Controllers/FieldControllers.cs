using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniFootballBooking.Services;

namespace MiniFootballBooking.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Manager,Staff")]
public class SchedulesController : Controller
{
    private readonly Data.ApplicationDbContext _db;
    private readonly IBookingService _booking;

    public SchedulesController(Data.ApplicationDbContext db, IBookingService booking)
    {
        _db = db;
        _booking = booking;
    }

    public async Task<IActionResult> Index(DateOnly? date, int? fieldId, string mode = "day")
    {
        var selected = date ?? AppClock.Today;
        var model = new SchedulePageViewModel
        {
            Date = selected,
            FieldId = fieldId,
            Mode = mode,
            Fields = await _db.FootballFields.OrderBy(f => f.FieldCode).ToListAsync()
        };

        if (mode == "week")
        {
            var offset = ((int)selected.DayOfWeek + 6) % 7;
            var start = selected.AddDays(-offset);
            var end = start.AddDays(6);
            var bookings = await _db.Bookings.Include(b => b.Field).Include(b => b.Customer)
                .Where(b => b.BookingDate >= start && b.BookingDate <= end && b.Status != BookingStatus.Cancelled)
                .OrderBy(b => b.StartTime).ToListAsync();
            for (var i = 0; i < 7; i++)
            {
                var day = start.AddDays(i);
                model.Week.Add(new WeekDayColumn
                {
                    Date = day,
                    Items = bookings.Where(b => b.BookingDate == day).Select(Map).ToList(),
                    BookingCount = bookings.Count(b => b.BookingDate == day)
                });
            }
        }
        else if (mode == "month")
        {
            var first = new DateOnly(selected.Year, selected.Month, 1);
            var start = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
            var counts = await _db.Bookings.Where(b => b.BookingDate >= first && b.BookingDate < first.AddMonths(1) && b.Status != BookingStatus.Cancelled)
                .GroupBy(b => b.BookingDate).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
            model.MonthStart = first;
            for (var i = 0; i < 42; i++)
            {
                var day = start.AddDays(i);
                model.Month.Add(new MonthCell
                {
                    Date = day,
                    InMonth = day.Month == first.Month,
                    BookingCount = counts.FirstOrDefault(c => c.Key == day)?.Count ?? 0
                });
            }
        }
        else
        {
            model.Schedules = await _booking.GetDayScheduleAsync(selected, fieldId);
        }

        return View(model);
    }

    private static BookingListItemViewModel Map(Booking b) => new()
    {
        Id = b.Id,
        BookingCode = b.BookingCode,
        FieldName = b.Field?.Name ?? "",
        CustomerName = b.Customer?.FullName ?? "",
        Phone = b.Customer?.PhoneNumber ?? "",
        BookingDate = b.BookingDate,
        StartTime = b.StartTime,
        EndTime = b.EndTime,
        TotalAmount = b.TotalAmount,
        Status = b.Status,
        PaymentStatus = b.PaymentStatus
    };
}

[Area("Admin")]
[Authorize(Roles = "Admin,Manager,Staff")]
public class FieldsController : Controller
{
    private readonly Data.ApplicationDbContext _db;
    private readonly ImageStorage _images;
    private readonly IAuditService _audit;
    private readonly Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _users;

    public FieldsController(Data.ApplicationDbContext db, ImageStorage images, IAuditService audit, Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> users)
    {
        _db = db;
        _images = images;
        _audit = audit;
        _users = users;
    }

    public async Task<IActionResult> Index(string? q, int? typeId, FieldStatus? status, int page = 1)
    {
        var query = _db.FootballFields.Include(f => f.FieldType).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(f => f.Name.Contains(q) || f.FieldCode.Contains(q) || f.Area.Contains(q));
        }

        if (typeId != null)
        {
            query = query.Where(f => f.FieldTypeId == typeId);
        }

        if (status != null)
        {
            query = query.Where(f => f.Status == status);
        }

        page = page < 1 ? 1 : page;
        var total = await query.CountAsync();
        ViewBag.Paging = new PagingModel { Page = page, TotalItems = total, TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)AppConstants.PageSize)) };
        ViewBag.Types = await _db.FieldTypes.OrderBy(t => t.Name).ToListAsync();
        ViewBag.Keyword = q;
        ViewBag.TypeId = typeId;
        ViewBag.Status = status;
        var items = await query.OrderBy(f => f.FieldCode).Skip((page - 1) * AppConstants.PageSize).Take(AppConstants.PageSize).ToListAsync();
        return View(items);
    }

    public async Task<IActionResult> Details(int id)
    {
        var field = await _db.FootballFields.Include(f => f.FieldType).Include(f => f.Images).Include(f => f.PriceRules)
            .FirstOrDefaultAsync(f => f.Id == id);
        return field == null ? NotFound() : View(field);
    }

    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Create() => View(await Form(new FieldFormViewModel()));

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FieldFormViewModel model, List<IFormFile> images)
    {
        if (await _db.FootballFields.AnyAsync(f => f.FieldCode == model.FieldCode))
        {
            ModelState.AddModelError(nameof(model.FieldCode), "Mã sân đã tồn tại.");
        }

        if (!ModelState.IsValid)
        {
            return View(await Form(model));
        }

        var field = new FootballField { CreatedAt = AppClock.Now };
        Apply(field, model);
        var order = 0;
        foreach (var image in images.Where(i => i.Length > 0))
        {
            var saved = await _images.SaveAsync(image, "fields");
            if (!saved.Ok)
            {
                ModelState.AddModelError(string.Empty, saved.Error);
                return View(await Form(model));
            }

            field.Images.Add(new FieldImage { ImageUrl = saved.Path, IsPrimary = order == 0, SortOrder = order });
            if (order == 0)
            {
                field.ImageUrl = saved.Path;
            }

            order++;
        }

        _db.FootballFields.Add(field);
        await _db.SaveChangesAsync();
        var actor = await _users.GetUserAsync(User);
        await _audit.LogAsync(actor?.Id, "CREATE FIELD", "FootballField", field.Id.ToString(), null, new { field.FieldCode, field.Name, field.BasePrice });
        TempData["Success"] = "Đã thêm sân bóng.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Edit(int id)
    {
        var field = await _db.FootballFields.Include(f => f.Images).FirstOrDefaultAsync(f => f.Id == id);
        if (field == null)
        {
            return NotFound();
        }

        return View(await Form(Map(field)));
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(FieldFormViewModel model, List<IFormFile> images)
    {
        var field = await _db.FootballFields.Include(f => f.Images).FirstOrDefaultAsync(f => f.Id == model.Id);
        if (field == null)
        {
            return NotFound();
        }

        if (await _db.FootballFields.AnyAsync(f => f.FieldCode == model.FieldCode && f.Id != model.Id))
        {
            ModelState.AddModelError(nameof(model.FieldCode), "Mã sân đã tồn tại.");
        }

        if (!ModelState.IsValid)
        {
            model.Images = field.Images.ToList();
            return View(await Form(model));
        }

        var oldPrice = field.BasePrice;
        Apply(field, model);
        field.UpdatedAt = AppClock.Now;
        var order = field.Images.Count;
        foreach (var image in images.Where(i => i.Length > 0))
        {
            var saved = await _images.SaveAsync(image, "fields");
            if (!saved.Ok)
            {
                ModelState.AddModelError(string.Empty, saved.Error);
                model.Images = field.Images.ToList();
                return View(await Form(model));
            }

            var primary = field.Images.Count == 0;
            field.Images.Add(new FieldImage { ImageUrl = saved.Path, IsPrimary = primary, SortOrder = order++ });
            if (primary || string.IsNullOrEmpty(field.ImageUrl))
            {
                field.ImageUrl = saved.Path;
            }
        }

        await _db.SaveChangesAsync();
        var actor = await _users.GetUserAsync(User);
        await _audit.LogAsync(actor?.Id, "UPDATE FIELD", "FootballField", field.Id.ToString(), new { BasePrice = oldPrice }, new { field.BasePrice, field.Status, field.Name });
        TempData["Success"] = "Đã cập nhật sân.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var field = await _db.FootballFields.FirstOrDefaultAsync(f => f.Id == id);
        if (field == null)
        {
            return NotFound();
        }

        field.IsActive = false;
        field.Status = FieldStatus.Inactive;
        field.UpdatedAt = AppClock.Now;
        await _db.SaveChangesAsync();
        var actor = await _users.GetUserAsync(User);
        await _audit.LogAsync(actor?.Id, "DELETE FIELD", "FootballField", field.Id.ToString(), new { field.IsActive }, new { IsActive = false });
        TempData["Success"] = "Đã khóa sân (xóa mềm). Dữ liệu booking được giữ lại.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lock(int id, bool open)
    {
        var field = await _db.FootballFields.FirstOrDefaultAsync(f => f.Id == id);
        if (field == null)
        {
            return NotFound();
        }

        field.IsActive = open;
        field.Status = open ? FieldStatus.Available : FieldStatus.Inactive;
        field.UpdatedAt = AppClock.Now;
        await _db.SaveChangesAsync();
        TempData["Success"] = open ? "Đã mở sân." : "Đã khóa sân.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int id)
    {
        var image = await _db.FieldImages.Include(i => i.Field).FirstOrDefaultAsync(i => i.Id == id);
        if (image == null)
        {
            return NotFound();
        }

        var fieldId = image.FieldId;
        _db.FieldImages.Remove(image);
        if (image.IsPrimary && image.Field != null)
        {
            image.Field.ImageUrl = image.Field.Images.OrderBy(i => i.SortOrder).FirstOrDefault(i => i.Id != id)?.ImageUrl;
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id = fieldId });
    }

    private async Task<FieldFormViewModel> Form(FieldFormViewModel model)
    {
        model.Types = await _db.FieldTypes.Where(t => t.IsActive).OrderBy(t => t.Capacity).ToListAsync();
        return model;
    }

    private static void Apply(FootballField field, FieldFormViewModel model)
    {
        field.FieldCode = model.FieldCode.Trim().ToUpperInvariant();
        field.Name = model.Name.Trim();
        field.FieldTypeId = model.FieldTypeId;
        field.Description = model.Description;
        field.Capacity = model.Capacity;
        field.Length = model.Length;
        field.Width = model.Width;
        field.SurfaceType = model.SurfaceType;
        field.BasePrice = model.BasePrice;
        field.Address = model.Address.Trim();
        field.Area = model.Area.Trim();
        field.Status = model.Status;
        field.HasLighting = model.HasLighting;
        field.HasChangingRoom = model.HasChangingRoom;
        field.HasToilet = model.HasToilet;
        field.HasParking = model.HasParking;
        field.HasWifi = model.HasWifi;
        field.HasWater = model.HasWater;
        field.IsActive = model.IsActive && model.Status != FieldStatus.Inactive;
    }

    private static FieldFormViewModel Map(FootballField field) => new()
    {
        Id = field.Id,
        FieldCode = field.FieldCode,
        Name = field.Name,
        FieldTypeId = field.FieldTypeId,
        Description = field.Description,
        Capacity = field.Capacity,
        Length = field.Length,
        Width = field.Width,
        SurfaceType = field.SurfaceType,
        BasePrice = field.BasePrice,
        Address = field.Address,
        Area = field.Area,
        Status = field.Status,
        HasLighting = field.HasLighting,
        HasChangingRoom = field.HasChangingRoom,
        HasToilet = field.HasToilet,
        HasParking = field.HasParking,
        HasWifi = field.HasWifi,
        HasWater = field.HasWater,
        IsActive = field.IsActive,
        ImageUrl = field.ImageUrl,
        Images = field.Images.OrderBy(i => i.SortOrder).ToList()
    };
}

[Area("Admin")]
[Authorize(Roles = "Admin,Manager")]
public class FieldTypesController : Controller
{
    private readonly Data.ApplicationDbContext _db;

    public FieldTypesController(Data.ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index() => View(await _db.FieldTypes.OrderBy(t => t.Capacity).ToListAsync());

    public IActionResult Create() => View(new FieldType());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FieldType model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        _db.FieldTypes.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã thêm loại sân.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await _db.FieldTypes.FindAsync(id);
        return item == null ? NotFound() : View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(FieldType model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        _db.FieldTypes.Update(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật loại sân.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.FieldTypes.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }

        if (await _db.FootballFields.AnyAsync(f => f.FieldTypeId == id))
        {
            item.IsActive = false;
            TempData["Success"] = "Loại sân đang được dùng nên chỉ ngừng kích hoạt.";
        }
        else
        {
            _db.FieldTypes.Remove(item);
            TempData["Success"] = "Đã xóa loại sân.";
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}

[Area("Admin")]
[Authorize(Roles = "Admin,Manager")]
public class PriceRulesController : Controller
{
    private readonly Data.ApplicationDbContext _db;

    public PriceRulesController(Data.ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int? fieldId, int page = 1)
    {
        var query = _db.PriceRules.Include(r => r.Field).AsQueryable();
        if (fieldId != null)
        {
            query = query.Where(r => r.FieldId == fieldId);
        }

        page = page < 1 ? 1 : page;
        var total = await query.CountAsync();
        ViewBag.Paging = new PagingModel { Page = page, TotalItems = total, TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)AppConstants.PageSize)) };
        ViewBag.Fields = await _db.FootballFields.OrderBy(f => f.FieldCode).ToListAsync();
        ViewBag.FieldId = fieldId;
        return View(await query.OrderBy(r => r.Field!.FieldCode).ThenBy(r => r.DayOfWeek).ThenBy(r => r.StartTime)
            .Skip((page - 1) * AppConstants.PageSize).Take(AppConstants.PageSize).ToListAsync());
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Fields = await _db.FootballFields.OrderBy(f => f.FieldCode).ToListAsync();
        return View(new PriceRule { StartTime = new TimeOnly(6, 0), EndTime = new TimeOnly(16, 0), EffectiveFrom = AppClock.Today, IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PriceRule model)
    {
        var error = await Validate(model);
        if (error != null)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Fields = await _db.FootballFields.OrderBy(f => f.FieldCode).ToListAsync();
            return View(model);
        }

        _db.PriceRules.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã thêm bảng giá.";
        return RedirectToAction(nameof(Index), new { fieldId = model.FieldId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await _db.PriceRules.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }

        ViewBag.Fields = await _db.FootballFields.OrderBy(f => f.FieldCode).ToListAsync();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PriceRule model)
    {
        var error = await Validate(model);
        if (error != null)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Fields = await _db.FootballFields.OrderBy(f => f.FieldCode).ToListAsync();
            return View(model);
        }

        _db.PriceRules.Update(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật bảng giá.";
        return RedirectToAction(nameof(Index), new { fieldId = model.FieldId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.PriceRules.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }

        _db.PriceRules.Remove(item);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã xóa khung giá.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<string?> Validate(PriceRule model)
    {
        if (model.EndTime <= model.StartTime)
        {
            return "Giờ kết thúc phải lớn hơn giờ bắt đầu.";
        }

        var overlap = await _db.PriceRules.AnyAsync(r => r.Id != model.Id && r.FieldId == model.FieldId && r.DayOfWeek == model.DayOfWeek && r.IsActive && model.IsActive && r.StartTime < model.EndTime && r.EndTime > model.StartTime);
        return overlap ? "Khung giờ bị trùng bảng giá khác của sân này." : null;
    }
}
