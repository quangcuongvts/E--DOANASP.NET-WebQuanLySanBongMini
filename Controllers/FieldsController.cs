namespace MiniFootballBooking.Controllers;

public class FieldsController : Controller
{
    private readonly Data.ApplicationDbContext _db;
    private readonly Services.IBookingService _booking;

    public FieldsController(Data.ApplicationDbContext db, Services.IBookingService booking)
    {
        _db = db;
        _booking = booking;
    }

    public async Task<IActionResult> Index(string? q, int? typeId, DateOnly? date, TimeOnly? start, TimeOnly? end, decimal? minPrice, decimal? maxPrice, FieldStatus? status, string? area, string sort = "popular", int page = 1)
    {
        if (!string.IsNullOrWhiteSpace(q))
        {
            HttpContext.Session.SetString("lastFieldSearch", q);
        }

        var query = _db.FootballFields.AsNoTracking().Where(f => f.IsActive && f.Status != FieldStatus.Inactive);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim();
            query = query.Where(f => f.Name.Contains(keyword) || f.FieldCode.Contains(keyword) || f.Address.Contains(keyword));
        }

        if (typeId != null)
        {
            query = query.Where(f => f.FieldTypeId == typeId);
        }

        if (!string.IsNullOrWhiteSpace(area))
        {
            query = query.Where(f => f.Area == area);
        }

        if (minPrice != null)
        {
            query = query.Where(f => f.BasePrice >= minPrice);
        }

        if (maxPrice != null)
        {
            query = query.Where(f => f.BasePrice <= maxPrice);
        }

        var items = await query.Select(f => new FieldCardViewModel
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
            BookingCount = f.Bookings.Count(b => b.Status != BookingStatus.Cancelled),
            HasLighting = f.HasLighting,
            HasParking = f.HasParking
        }).ToListAsync();

        if (date != null && start != null && end != null && end > start)
        {
            var startDt = date.Value.ToDateTime(start.Value);
            var endDt = date.Value.ToDateTime(end.Value);
            var busy = await _db.Bookings.Where(b => b.BookingDate == date && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.NoShow && b.StartTime < end && b.EndTime > start)
                .Select(b => b.FieldId).ToListAsync();
            var maintained = await _db.Maintenances.Where(m => m.Status != MaintenanceStatus.Cancelled && m.StartDateTime < endDt && m.EndDateTime > startDt)
                .Select(m => m.FieldId).ToListAsync();
            items = items.Where(f => f.Status != FieldStatus.Maintenance && !busy.Contains(f.Id) && !maintained.Contains(f.Id)).ToList();
        }

        var today = AppClock.Today;
        var time = TimeOnly.FromDateTime(AppClock.Now);
        var occupied = await _db.Bookings.Where(b => b.BookingDate == today && b.StartTime <= time && b.EndTime > time
                && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Playing))
            .Select(b => b.FieldId).ToListAsync();
        foreach (var item in items)
        {
            item.DisplayStatus = item.Status == FieldStatus.Maintenance
                ? FieldStatus.Maintenance
                : occupied.Contains(item.Id) ? FieldStatus.Occupied : FieldStatus.Available;
        }

        if (status != null)
        {
            items = items.Where(f => f.DisplayStatus == status).ToList();
        }

        items = sort switch
        {
            "price_asc" => items.OrderBy(f => f.BasePrice).ToList(),
            "price_desc" => items.OrderByDescending(f => f.BasePrice).ToList(),
            "rating" => items.OrderByDescending(f => f.RatingAverage).ThenByDescending(f => f.RatingCount).ToList(),
            _ => items.OrderByDescending(f => f.BookingCount).ThenByDescending(f => f.RatingAverage).ToList()
        };

        page = page < 1 ? 1 : page;
        var model = new FieldFilterViewModel
        {
            Keyword = q,
            TypeId = typeId,
            Date = date,
            Start = start,
            End = end,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            Status = status,
            Area = area,
            Sort = sort,
            Page = page,
            TotalItems = items.Count,
            TotalPages = Math.Max(1, (int)Math.Ceiling(items.Count / (double)AppConstants.PageSize)),
            Fields = items.Skip((page - 1) * AppConstants.PageSize).Take(AppConstants.PageSize).ToList(),
            Types = await _db.FieldTypes.Where(t => t.IsActive).OrderBy(t => t.Capacity).ToListAsync(),
            Areas = await _db.FootballFields.Where(f => f.IsActive).Select(f => f.Area).Distinct().OrderBy(a => a).ToListAsync()
        };
        return View(model);
    }

    public async Task<IActionResult> Details(int id, DateOnly? date)
    {
        var field = await _db.FootballFields.AsNoTracking()
            .Include(f => f.FieldType)
            .Include(f => f.Images)
            .Include(f => f.PriceRules)
            .FirstOrDefaultAsync(f => f.Id == id && f.IsActive);
        if (field == null)
        {
            return NotFound();
        }

        var selected = date ?? AppClock.Today;
        var reviews = await _db.Reviews.AsNoTracking().Include(r => r.Customer)
            .Where(r => r.FieldId == id && r.IsVisible)
            .OrderByDescending(r => r.CreatedAt)
            .Take(12)
            .Select(r => new ReviewCardViewModel
            {
                Id = r.Id,
                CustomerName = r.Customer!.FullName,
                AvatarUrl = r.Customer.AvatarUrl,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt,
                FieldName = field.Name
            }).ToListAsync();

        var time = TimeOnly.FromDateTime(AppClock.Now);
        var busy = await _db.Bookings.AnyAsync(b => b.FieldId == id && b.BookingDate == AppClock.Today && b.StartTime <= time && b.EndTime > time
            && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Playing));
        var dow = (int)selected.DayOfWeek;
        var model = new FieldDetailViewModel
        {
            Id = field.Id,
            FieldCode = field.FieldCode,
            Name = field.Name,
            TypeName = field.FieldType?.Name ?? "",
            Capacity = field.Capacity,
            Length = field.Length,
            Width = field.Width,
            SurfaceType = field.SurfaceType,
            BasePrice = field.BasePrice,
            Address = field.Address,
            Area = field.Area,
            Description = field.Description,
            ImageUrl = field.ImageUrl,
            DisplayStatus = field.Status == FieldStatus.Maintenance ? FieldStatus.Maintenance : busy ? FieldStatus.Occupied : FieldStatus.Available,
            RatingAverage = field.RatingAverage,
            RatingCount = await _db.Reviews.CountAsync(r => r.FieldId == id && r.IsVisible),
            HasLighting = field.HasLighting,
            HasChangingRoom = field.HasChangingRoom,
            HasToilet = field.HasToilet,
            HasParking = field.HasParking,
            HasWifi = field.HasWifi,
            HasWater = field.HasWater,
            Images = field.Images.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).ToList(),
            PriceRules = field.PriceRules.Where(r => r.IsActive && r.DayOfWeek == dow).OrderBy(r => r.StartTime).ToList(),
            Reviews = reviews,
            Slots = await _booking.GetSlotsAsync(id, selected),
            SelectedDate = selected
        };
        if (model.Images.Count == 0 && !string.IsNullOrEmpty(field.ImageUrl))
        {
            model.Images.Add(field.ImageUrl);
        }

        return View(model);
    }
}
