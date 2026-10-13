using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using MiniFootballBooking.Services;

namespace MiniFootballBooking.Controllers;

public class PostsController : Controller
{
    private readonly Data.ApplicationDbContext _db;

    public PostsController(Data.ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        page = page < 1 ? 1 : page;
        var query = _db.Posts.AsNoTracking().Where(p => p.IsPublished).OrderByDescending(p => p.PublishedAt);
        var total = await query.CountAsync();
        ViewBag.Paging = new PagingModel
        {
            Page = page,
            TotalItems = total,
            TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)AppConstants.PageSize))
        };
        var posts = await query.Skip((page - 1) * AppConstants.PageSize).Take(AppConstants.PageSize).ToListAsync();
        return View(posts);
    }

    public async Task<IActionResult> Details(string id)
    {
        var post = await _db.Posts.Include(p => p.Author).FirstOrDefaultAsync(p => p.Slug == id && p.IsPublished);
        return post == null ? NotFound() : View(post);
    }
}

public class PromotionsController : Controller
{
    private readonly Data.ApplicationDbContext _db;

    public PromotionsController(Data.ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var vouchers = await _db.Vouchers.AsNoTracking()
            .Where(v => v.IsActive && v.EndDate >= AppClock.Now)
            .OrderBy(v => v.EndDate)
            .ToListAsync();
        return View(vouchers);
    }
}

[Authorize]
public class NotificationsController : Controller
{
    private readonly Data.ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly INotificationService _notifications;

    public NotificationsController(Data.ApplicationDbContext db, UserManager<ApplicationUser> users, INotificationService notifications)
    {
        _db = db;
        _users = users;
        _notifications = notifications;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        if (User.IsInRole("Customer"))
        {
            await _notifications.EnsureUpcomingAsync(user.Id);
        }

        var items = await _db.Notifications.Where(n => n.UserId == user.Id)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationItemViewModel
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                Link = n.Link
            }).ToListAsync();
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var user = await _users.GetUserAsync(User);
        var item = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == user!.Id);
        if (item != null)
        {
            item.IsRead = true;
            await _db.SaveChangesAsync();
        }

        return Request.Headers.ContainsKey("RequestVerificationToken") && Request.Headers.Accept.ToString().Contains("application/json")
            ? Json(new { ok = true })
            : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAll()
    {
        var user = await _users.GetUserAsync(User);
        var items = await _db.Notifications.Where(n => n.UserId == user!.Id && !n.IsRead).ToListAsync();
        items.ForEach(n => n.IsRead = true);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}

[Authorize]
public class ReviewsController : Controller
{
    private readonly Data.ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly INotificationService _notifications;

    public ReviewsController(Data.ApplicationDbContext db, UserManager<ApplicationUser> users, INotificationService notifications)
    {
        _db = db;
        _users = users;
        _notifications = notifications;
    }

    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Create(int bookingId)
    {
        var user = await _users.GetUserAsync(User);
        var booking = await _db.Bookings.Include(b => b.Field).FirstOrDefaultAsync(b => b.Id == bookingId && b.CustomerId == user!.Id);
        if (booking == null || booking.Status != BookingStatus.Completed)
        {
            TempData["Error"] = "Chỉ đánh giá sau khi trận đã hoàn thành.";
            return RedirectToAction("Mine", "Booking");
        }

        if (await _db.Reviews.AnyAsync(r => r.BookingId == bookingId))
        {
            TempData["Error"] = "Bạn đã đánh giá đơn này.";
            return RedirectToAction("Details", "Booking", new { id = bookingId });
        }

        return View(new ReviewFormViewModel { BookingId = booking.Id, FieldName = booking.Field?.Name ?? "", BookingCode = booking.BookingCode });
    }

    [Authorize(Roles = "Customer")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReviewFormViewModel model)
    {
        var user = await _users.GetUserAsync(User);
        var booking = await _db.Bookings.Include(b => b.Field).FirstOrDefaultAsync(b => b.Id == model.BookingId && b.CustomerId == user!.Id);
        if (booking == null || booking.Status != BookingStatus.Completed)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            model.FieldName = booking.Field?.Name ?? "";
            model.BookingCode = booking.BookingCode;
            return View(model);
        }

        if (await _db.Reviews.AnyAsync(r => r.BookingId == booking.Id))
        {
            TempData["Error"] = "Bạn đã đánh giá đơn này.";
            return RedirectToAction("Details", "Booking", new { id = booking.Id });
        }

        _db.Reviews.Add(new Review
        {
            CustomerId = user!.Id,
            FieldId = booking.FieldId,
            BookingId = booking.Id,
            Rating = model.Rating,
            Comment = model.Comment.Trim(),
            CreatedAt = AppClock.Now,
            IsVisible = true
        });
        await _db.SaveChangesAsync();
        await RecalculateAsync(booking.FieldId);
        await _notifications.NotifyRolesAsync(["Admin", "Manager"], "Có đánh giá mới",
            $"{user.FullName} đánh giá {booking.Field?.Name} {model.Rating} sao.", NotificationType.NewReview, "/Admin/Reviews");
        TempData["Success"] = "Cảm ơn bạn đã đánh giá sân.";
        return RedirectToAction("Details", "Booking", new { id = booking.Id });
    }

    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _users.GetUserAsync(User);
        var review = await _db.Reviews.Include(r => r.Field).Include(r => r.Booking).FirstOrDefaultAsync(r => r.Id == id && r.CustomerId == user!.Id);
        if (review == null)
        {
            return NotFound();
        }

        return View(new ReviewFormViewModel
        {
            Id = review.Id,
            BookingId = review.BookingId,
            FieldName = review.Field?.Name ?? "",
            BookingCode = review.Booking?.BookingCode ?? "",
            Rating = review.Rating,
            Comment = review.Comment
        });
    }

    [Authorize(Roles = "Customer")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ReviewFormViewModel model)
    {
        var user = await _users.GetUserAsync(User);
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == model.Id && r.CustomerId == user!.Id);
        if (review == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        review.Rating = model.Rating;
        review.Comment = model.Comment.Trim();
        await _db.SaveChangesAsync();
        await RecalculateAsync(review.FieldId);
        TempData["Success"] = "Đã cập nhật đánh giá.";
        return RedirectToAction("Details", "Booking", new { id = review.BookingId });
    }

    [Authorize(Roles = "Customer")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _users.GetUserAsync(User);
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == id && r.CustomerId == user!.Id);
        if (review == null)
        {
            return NotFound();
        }

        var fieldId = review.FieldId;
        var bookingId = review.BookingId;
        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync();
        await RecalculateAsync(fieldId);
        TempData["Success"] = "Đã xóa đánh giá.";
        return RedirectToAction("Details", "Booking", new { id = bookingId });
    }

    private async Task RecalculateAsync(int fieldId)
    {
        var ratings = await _db.Reviews.Where(r => r.FieldId == fieldId && r.IsVisible).Select(r => r.Rating).ToListAsync();
        var field = await _db.FootballFields.FirstAsync(f => f.Id == fieldId);
        field.RatingAverage = ratings.Count == 0 ? 0 : Math.Round((decimal)ratings.Average(), 1);
        await _db.SaveChangesAsync();
    }
}
