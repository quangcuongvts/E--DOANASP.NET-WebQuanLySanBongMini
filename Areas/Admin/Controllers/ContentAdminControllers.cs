using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MiniFootballBooking.Services;

namespace MiniFootballBooking.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class PostsController : Controller
{
    private readonly Data.ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ImageStorage _images;

    public PostsController(Data.ApplicationDbContext db, UserManager<ApplicationUser> users, ImageStorage images)
    {
        _db = db;
        _users = users;
        _images = images;
    }

    public async Task<IActionResult> Index() => View(await _db.Posts.Include(p => p.Author).OrderByDescending(p => p.PublishedAt).ToListAsync());

    public IActionResult Create() => View(new Post { IsPublished = true });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Post model, IFormFile? thumbnail)
    {
        ModelState.Remove(nameof(Post.Slug));
        model.Slug = "tam";
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var actor = await _users.GetUserAsync(User);
        model.AuthorId = actor?.Id;
        model.Slug = await UniqueSlug(TextHelper.Slugify(model.Title), null);
        model.PublishedAt = model.IsPublished ? AppClock.Now : null;
        if (thumbnail != null)
        {
            var saved = await _images.SaveAsync(thumbnail, "posts");
            if (!saved.Ok)
            {
                ModelState.AddModelError(string.Empty, saved.Error);
                return View(model);
            }

            model.ThumbnailUrl = saved.Path;
        }

        _db.Posts.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã đăng bài viết.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var post = await _db.Posts.FindAsync(id);
        return post == null ? NotFound() : View(post);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Post model, IFormFile? thumbnail)
    {
        var post = await _db.Posts.FindAsync(model.Id);
        if (post == null)
        {
            return NotFound();
        }

        ModelState.Remove(nameof(Post.Slug));
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        post.Title = model.Title;
        post.Summary = model.Summary;
        post.Content = model.Content;
        post.IsPublished = model.IsPublished;
        post.Slug = await UniqueSlug(TextHelper.Slugify(model.Title), post.Id);
        post.PublishedAt = model.IsPublished ? post.PublishedAt ?? AppClock.Now : null;
        if (thumbnail != null)
        {
            var saved = await _images.SaveAsync(thumbnail, "posts");
            if (!saved.Ok)
            {
                ModelState.AddModelError(string.Empty, saved.Error);
                return View(model);
            }

            post.ThumbnailUrl = saved.Path;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật bài viết.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var post = await _db.Posts.FindAsync(id);
        if (post == null)
        {
            return NotFound();
        }

        _db.Posts.Remove(post);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã xóa bài viết.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<string> UniqueSlug(string slug, int? id)
    {
        var candidate = string.IsNullOrWhiteSpace(slug) ? "bai-viet" : slug;
        var origin = candidate;
        var i = 2;
        while (await _db.Posts.AnyAsync(p => p.Slug == candidate && p.Id != id))
        {
            candidate = origin + "-" + i++;
        }

        return candidate;
    }
}

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class BannersController : Controller
{
    private readonly Data.ApplicationDbContext _db;
    private readonly ImageStorage _images;

    public BannersController(Data.ApplicationDbContext db, ImageStorage images)
    {
        _db = db;
        _images = images;
    }

    public async Task<IActionResult> Index() => View(await _db.Banners.OrderBy(b => b.SortOrder).ToListAsync());

    public IActionResult Create() => View(new Banner { IsActive = true });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Banner model, IFormFile? image)
    {
        if (image != null)
        {
            var saved = await _images.SaveAsync(image, "banners");
            if (!saved.Ok)
            {
                ModelState.AddModelError(string.Empty, saved.Error);
            }
            else
            {
                model.ImageUrl = saved.Path;
                ModelState.Remove(nameof(Banner.ImageUrl));
            }
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        _db.Banners.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã thêm banner.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await _db.Banners.FindAsync(id);
        return item == null ? NotFound() : View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Banner model, IFormFile? image)
    {
        var item = await _db.Banners.FindAsync(model.Id);
        if (item == null)
        {
            return NotFound();
        }

        if (image != null)
        {
            var saved = await _images.SaveAsync(image, "banners");
            if (!saved.Ok)
            {
                ModelState.AddModelError(string.Empty, saved.Error);
                return View(model);
            }

            item.ImageUrl = saved.Path;
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        item.Title = model.Title;
        item.Subtitle = model.Subtitle;
        item.LinkUrl = model.LinkUrl;
        item.SortOrder = model.SortOrder;
        item.IsActive = model.IsActive;
        if (!string.IsNullOrWhiteSpace(model.ImageUrl) && image == null)
        {
            item.ImageUrl = model.ImageUrl;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật banner.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.Banners.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }

        _db.Banners.Remove(item);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã xóa banner.";
        return RedirectToAction(nameof(Index));
    }
}

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ContactsController : Controller
{
    private readonly Data.ApplicationDbContext _db;

    public ContactsController(Data.ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(ContactStatus? status, int page = 1)
    {
        var query = _db.Contacts.AsQueryable();
        if (status != null)
        {
            query = query.Where(c => c.Status == status);
        }

        page = page < 1 ? 1 : page;
        var total = await query.CountAsync();
        ViewBag.Paging = new PagingModel { Page = page, TotalItems = total, TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)AppConstants.PageSize)) };
        ViewBag.Status = status;
        return View(await query.OrderByDescending(c => c.CreatedAt).Skip((page - 1) * AppConstants.PageSize).Take(AppConstants.PageSize).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var item = await _db.Contacts.FindAsync(id);
        return item == null ? NotFound() : View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Process(int id)
    {
        var item = await _db.Contacts.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }

        item.Status = ContactStatus.Processed;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã đánh dấu xử lý.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.Contacts.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }

        _db.Contacts.Remove(item);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã xóa liên hệ.";
        return RedirectToAction(nameof(Index));
    }
}

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AuditLogsController : Controller
{
    private readonly Data.ApplicationDbContext _db;

    public AuditLogsController(Data.ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        var query = _db.AuditLogs.Include(a => a.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(a => a.Action.Contains(q) || a.EntityName.Contains(q) || (a.User != null && a.User.FullName.Contains(q)));
        }

        page = page < 1 ? 1 : page;
        var total = await query.CountAsync();
        ViewBag.Paging = new PagingModel { Page = page, TotalItems = total, TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)AppConstants.PageSize)) };
        ViewBag.Keyword = q;
        return View(await query.OrderByDescending(a => a.CreatedAt).Skip((page - 1) * AppConstants.PageSize).Take(AppConstants.PageSize).ToListAsync());
    }
}

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class SettingsController : Controller
{
    private readonly Data.ApplicationDbContext _db;

    public SettingsController(Data.ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index() => View(await _db.SystemSettings.OrderBy(s => s.Key).ToListAsync());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(Dictionary<string, string> settings)
    {
        var rows = await _db.SystemSettings.ToListAsync();
        foreach (var row in rows)
        {
            if (settings.TryGetValue(row.Key, out var value))
            {
                row.Value = value?.Trim() ?? "";
            }
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã lưu cấu hình.";
        return RedirectToAction(nameof(Index));
    }
}

[Area("Admin")]
[Authorize(Roles = "Admin,Manager")]
public class ReportsController : Controller
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports)
    {
        _reports = reports;
    }

    public Task<IActionResult> Index(DateOnly? from, DateOnly? to, int? fieldId, int? typeId, string focus = "revenue", int page = 1)
        => Build(from, to, fieldId, typeId, focus, page);

    public Task<IActionResult> Revenue(DateOnly? from, DateOnly? to, int? fieldId, int? typeId, int page = 1)
        => Build(from, to, fieldId, typeId, "revenue", page);

    public Task<IActionResult> Bookings(DateOnly? from, DateOnly? to, int? fieldId, int? typeId, int page = 1)
        => Build(from, to, fieldId, typeId, "booking", page);

    public Task<IActionResult> Customers(DateOnly? from, DateOnly? to, int? fieldId, int? typeId, int page = 1)
        => Build(from, to, fieldId, typeId, "customer", page);

    public Task<IActionResult> Performance(DateOnly? from, DateOnly? to, int? fieldId, int? typeId, int page = 1)
        => Build(from, to, fieldId, typeId, "performance", page);

    public async Task<IActionResult> Export(DateOnly? from, DateOnly? to, int? fieldId, int? typeId)
    {
        var start = from ?? new DateOnly(AppClock.Today.Year, AppClock.Today.Month, 1);
        var end = to ?? AppClock.Today;
        var rows = await _reports.GetReportBookingsAsync(start, end, fieldId, typeId);
        var builder = new StringBuilder();
        builder.AppendLine("Ma booking,San,Khach,Dien thoai,Ngay,Gio,Tong tien,Thanh toan,Trang thai");
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",",
                Csv(row.BookingCode), Csv(row.FieldName), Csv(row.CustomerName), Csv(row.Phone),
                row.BookingDate.ToString("dd/MM/yyyy"), $"{row.StartTime:HH:mm}-{row.EndTime:HH:mm}",
                row.TotalAmount.ToString("0"), row.PaymentStatus, row.Status));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
        return File(bytes, "text/csv", $"bao-cao-booking-{start:yyyyMMdd}-{end:yyyyMMdd}.csv");
    }

    private async Task<IActionResult> Build(DateOnly? from, DateOnly? to, int? fieldId, int? typeId, string focus, int page)
    {
        var start = from ?? new DateOnly(AppClock.Today.Year, AppClock.Today.Month, 1);
        var end = to ?? AppClock.Today;
        var model = await _reports.GetReportAsync(start, end, fieldId, typeId, focus, page);
        return View("Index", model);
    }

    private static string Csv(string? value)
    {
        var text = value ?? "";
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
