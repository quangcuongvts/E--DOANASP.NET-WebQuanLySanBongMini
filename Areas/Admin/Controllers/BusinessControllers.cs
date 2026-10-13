using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MiniFootballBooking.Services;

namespace MiniFootballBooking.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Manager,Staff")]
public class CustomersController : Controller
{
    private readonly Data.ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public CustomersController(Data.ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        var role = await _db.Roles.Where(r => r.Name == "Customer").Select(r => r.Id).FirstAsync();
        var ids = _db.UserRoles.Where(r => r.RoleId == role).Select(r => r.UserId);
        var query = _db.Users.Where(u => ids.Contains(u.Id));
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(u => u.FullName.Contains(q) || (u.Email != null && u.Email.Contains(q)) || (u.PhoneNumber != null && u.PhoneNumber.Contains(q)));
        }

        page = page < 1 ? 1 : page;
        var total = await query.CountAsync();
        ViewBag.Paging = new PagingModel { Page = page, TotalItems = total, TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)AppConstants.PageSize)) };
        ViewBag.Keyword = q;
        return View(await query.OrderByDescending(u => u.CreatedAt).Skip((page - 1) * AppConstants.PageSize).Take(AppConstants.PageSize).ToListAsync());
    }

    public async Task<IActionResult> Details(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null || !await _users.IsInRoleAsync(user, "Customer"))
        {
            return NotFound();
        }

        ViewBag.Bookings = await _db.Bookings.Include(b => b.Field).Where(b => b.CustomerId == id).OrderByDescending(b => b.BookingDate).Take(30).ToListAsync();
        return View(user);
    }
}

[Area("Admin")]
[Authorize(Roles = "Admin,Manager")]
public class StaffsController : Controller
{
    private readonly Data.ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public StaffsController(Data.ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IActionResult> Index()
    {
        var role = await _db.Roles.Where(r => r.Name == "Staff").Select(r => r.Id).FirstAsync();
        var ids = _db.UserRoles.Where(r => r.RoleId == role).Select(r => r.UserId);
        return View(await _db.Users.Where(u => ids.Contains(u.Id)).OrderBy(u => u.FullName).ToListAsync());
    }

    public async Task<IActionResult> Details(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        ViewBag.Handled = await _db.Bookings.Include(b => b.Field).Include(b => b.Customer)
            .Where(b => b.StaffId == id).OrderByDescending(b => b.BookingDate).Take(20).ToListAsync();
        return View(user);
    }
}

[Area("Admin")]
[Authorize(Roles = "Admin,Manager,Staff")]
public class PaymentsController : Controller
{
    private readonly Data.ApplicationDbContext _db;

    public PaymentsController(Data.ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? q, PaymentStatus? status, PaymentMethod? method, int page = 1)
    {
        var query = _db.Payments.Include(p => p.Booking).ThenInclude(b => b!.Customer).Include(p => p.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(p => p.PaymentCode.Contains(q) || (p.Booking != null && p.Booking.BookingCode.Contains(q)));
        }

        if (status != null)
        {
            query = query.Where(p => p.PaymentStatus == status);
        }

        if (method != null)
        {
            query = query.Where(p => p.PaymentMethod == method);
        }

        page = page < 1 ? 1 : page;
        var total = await query.CountAsync();
        ViewBag.Paging = new PagingModel { Page = page, TotalItems = total, TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)AppConstants.PageSize)) };
        ViewBag.Keyword = q;
        ViewBag.Status = status;
        ViewBag.Method = method;
        return View(await query.OrderByDescending(p => p.Id).Skip((page - 1) * AppConstants.PageSize).Take(AppConstants.PageSize).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var payment = await _db.Payments.Include(p => p.Booking).ThenInclude(b => b!.Field).Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
        return payment == null ? NotFound() : View(payment);
    }
}

[Area("Admin")]
[Authorize(Roles = "Admin,Manager")]
public class ServicesController : Controller
{
    private readonly Data.ApplicationDbContext _db;
    private readonly ImageStorage _images;

    public ServicesController(Data.ApplicationDbContext db, ImageStorage images)
    {
        _db = db;
        _images = images;
    }

    public async Task<IActionResult> Index(string? q) 
    {
        var query = _db.ServiceItems.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(s => s.Name.Contains(q));
        }

        ViewBag.Keyword = q;
        return View(await query.OrderBy(s => s.Name).ToListAsync());
    }

    public IActionResult Create() => View(new ServiceItem());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceItem model, IFormFile? image)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (image != null)
        {
            var saved = await _images.SaveAsync(image, "services");
            if (!saved.Ok)
            {
                ModelState.AddModelError(string.Empty, saved.Error);
                return View(model);
            }

            model.ImageUrl = saved.Path;
        }

        model.CreatedAt = AppClock.Now;
        _db.ServiceItems.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã thêm dịch vụ.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await _db.ServiceItems.FindAsync(id);
        return item == null ? NotFound() : View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ServiceItem model, IFormFile? image)
    {
        var item = await _db.ServiceItems.FindAsync(model.Id);
        if (item == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        item.Name = model.Name;
        item.Description = model.Description;
        item.Price = model.Price;
        item.IsActive = model.IsActive;
        if (image != null)
        {
            var saved = await _images.SaveAsync(image, "services");
            if (!saved.Ok)
            {
                ModelState.AddModelError(string.Empty, saved.Error);
                return View(model);
            }

            item.ImageUrl = saved.Path;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật dịch vụ.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.ServiceItems.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }

        if (await _db.BookingServiceLines.AnyAsync(l => l.ServiceId == id))
        {
            item.IsActive = false;
            TempData["Success"] = "Dịch vụ đã phát sinh booking nên chỉ ngừng kinh doanh.";
        }
        else
        {
            _db.ServiceItems.Remove(item);
            TempData["Success"] = "Đã xóa dịch vụ.";
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}

[Area("Admin")]
[Authorize(Roles = "Admin,Manager")]
public class VouchersController : Controller
{
    private readonly Data.ApplicationDbContext _db;

    public VouchersController(Data.ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        var query = _db.Vouchers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(v => v.VoucherCode.Contains(q) || v.Name.Contains(q));
        }

        page = page < 1 ? 1 : page;
        var total = await query.CountAsync();
        ViewBag.Paging = new PagingModel { Page = page, TotalItems = total, TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)AppConstants.PageSize)) };
        ViewBag.Keyword = q;
        return View(await query.OrderByDescending(v => v.Id).Skip((page - 1) * AppConstants.PageSize).Take(AppConstants.PageSize).ToListAsync());
    }

    public IActionResult Create() => View(new Voucher { StartDate = AppClock.Now, EndDate = AppClock.Now.AddMonths(3), UsageLimit = 100, MaxUsagePerUser = 1, IsActive = true });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Voucher model)
    {
        model.VoucherCode = model.VoucherCode.Trim().ToUpperInvariant();
        if (await _db.Vouchers.AnyAsync(v => v.VoucherCode == model.VoucherCode))
        {
            ModelState.AddModelError(nameof(model.VoucherCode), "Mã voucher đã tồn tại.");
        }

        if (model.EndDate <= model.StartDate)
        {
            ModelState.AddModelError(nameof(model.EndDate), "Ngày kết thúc phải sau ngày bắt đầu.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        _db.Vouchers.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã tạo voucher.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await _db.Vouchers.FindAsync(id);
        return item == null ? NotFound() : View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Voucher model)
    {
        var item = await _db.Vouchers.FindAsync(model.Id);
        if (item == null)
        {
            return NotFound();
        }

        model.VoucherCode = model.VoucherCode.Trim().ToUpperInvariant();
        if (await _db.Vouchers.AnyAsync(v => v.VoucherCode == model.VoucherCode && v.Id != model.Id))
        {
            ModelState.AddModelError(nameof(model.VoucherCode), "Mã voucher đã tồn tại.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        item.VoucherCode = model.VoucherCode;
        item.Name = model.Name;
        item.Description = model.Description;
        item.DiscountType = model.DiscountType;
        item.DiscountValue = model.DiscountValue;
        item.MinOrderAmount = model.MinOrderAmount;
        item.MaxDiscountAmount = model.MaxDiscountAmount;
        item.StartDate = model.StartDate;
        item.EndDate = model.EndDate;
        item.UsageLimit = model.UsageLimit;
        item.MaxUsagePerUser = model.MaxUsagePerUser;
        item.IsActive = model.IsActive;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật voucher.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.Vouchers.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }

        if (item.UsedCount > 0)
        {
            item.IsActive = false;
            TempData["Success"] = "Voucher đã được dùng nên chỉ ngừng áp dụng.";
        }
        else
        {
            _db.Vouchers.Remove(item);
            TempData["Success"] = "Đã xóa voucher.";
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
