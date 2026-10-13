using Microsoft.AspNetCore.Identity;

namespace MiniFootballBooking.Controllers;

public class HomeController : Controller
{
    private readonly Data.ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<HomeController> _logger;

    public HomeController(Data.ApplicationDbContext db, UserManager<ApplicationUser> users, ILogger<HomeController> logger)
    {
        _db = db;
        _users = users;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var today = AppClock.Today;
        var time = TimeOnly.FromDateTime(AppClock.Now);
        var busyIds = await _db.Bookings.Where(b => b.BookingDate == today && b.StartTime <= time && b.EndTime > time
                && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.NoShow && b.Status != BookingStatus.Pending)
            .Select(b => b.FieldId).Distinct().ToListAsync();

        var fields = await _db.FootballFields.AsNoTracking()
            .Include(f => f.FieldType)
            .Where(f => f.IsActive && f.Status != FieldStatus.Inactive)
            .OrderByDescending(f => f.RatingAverage)
            .Take(6)
            .Select(f => new FieldCardViewModel
            {
                Id = f.Id,
                FieldCode = f.FieldCode,
                Name = f.Name,
                TypeName = f.FieldType!.Name,
                Capacity = f.Capacity,
                BasePrice = f.BasePrice,
                RatingAverage = f.RatingAverage,
                RatingCount = f.Reviews.Count(r => r.IsVisible),
                ImageUrl = f.ImageUrl,
                Area = f.Area,
                Address = f.Address,
                Status = f.Status,
                BookingCount = f.Bookings.Count(b => b.Status != BookingStatus.Cancelled)
            }).ToListAsync();

        foreach (var field in fields)
        {
            field.DisplayStatus = field.Status == FieldStatus.Maintenance
                ? FieldStatus.Maintenance
                : busyIds.Contains(field.Id) ? FieldStatus.Occupied : FieldStatus.Available;
        }

        var customerRole = await _db.Roles.Where(r => r.Name == "Customer").Select(r => r.Id).FirstAsync();
        var model = new HomeViewModel
        {
            FeaturedFields = fields,
            Services = await _db.ServiceItems.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Price).ToListAsync(),
            Vouchers = await _db.Vouchers.AsNoTracking().Where(v => v.IsActive && v.EndDate >= AppClock.Now).OrderBy(v => v.DiscountValue).ToListAsync(),
            Reviews = await _db.Reviews.AsNoTracking().Include(r => r.Customer).Include(r => r.Field)
                .Where(r => r.IsVisible).OrderByDescending(r => r.CreatedAt).Take(6)
                .Select(r => new ReviewCardViewModel
                {
                    CustomerName = r.Customer!.FullName,
                    AvatarUrl = r.Customer.AvatarUrl,
                    FieldName = r.Field!.Name,
                    FieldId = r.FieldId,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt
                }).ToListAsync(),
            Posts = await _db.Posts.AsNoTracking().Where(p => p.IsPublished).OrderByDescending(p => p.PublishedAt).Take(3).ToListAsync(),
            Banners = await _db.Banners.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.SortOrder).ToListAsync(),
            FieldCount = await _db.FootballFields.CountAsync(f => f.IsActive),
            CustomerCount = await _db.UserRoles.CountAsync(r => r.RoleId == customerRole),
            BookingCount = await _db.Bookings.CountAsync(b => b.Status != BookingStatus.Cancelled),
            LastSearch = HttpContext.Session.GetString("lastFieldSearch")
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Contact()
    {
        var model = new ContactFormViewModel();
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _users.GetUserAsync(User);
            if (user != null)
            {
                model.FullName = user.FullName;
                model.Email = user.Email ?? "";
                model.Phone = user.PhoneNumber ?? "";
            }
        }

        ViewBag.Settings = await _db.SystemSettings.ToDictionaryAsync(s => s.Key, s => s.Value);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(ContactFormViewModel model)
    {
        ViewBag.Settings = await _db.SystemSettings.ToDictionaryAsync(s => s.Key, s => s.Value);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        _db.Contacts.Add(new ContactMessage
        {
            FullName = model.FullName.Trim(),
            Email = model.Email.Trim(),
            Phone = model.Phone.Trim(),
            Subject = model.Subject.Trim(),
            Message = model.Message.Trim(),
            Status = ContactStatus.New,
            CreatedAt = AppClock.Now
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã gửi liên hệ. Chúng tôi sẽ phản hồi sớm.";
        return RedirectToAction(nameof(Contact));
    }

    [Route("Home/StatusCode")]
    public IActionResult Status(int code)
    {
        Response.StatusCode = code;
        return View("StatusCode", code);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var feature = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        if (feature?.Error != null)
        {
            _logger.LogError(feature.Error, "Unhandled exception at {Path}", feature.Path);
        }

        return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
