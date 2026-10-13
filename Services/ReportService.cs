using Microsoft.AspNetCore.Identity;
using MiniFootballBooking.Data;

namespace MiniFootballBooking.Services;

public interface IReportService
{
    Task<AdminDashboardViewModel> GetDashboardAsync(bool staffOnly);
    Task<ReportViewModel> GetReportAsync(DateOnly from, DateOnly to, int? fieldId, int? typeId, string focus, int page);
    Task<List<BookingListItemViewModel>> GetReportBookingsAsync(DateOnly from, DateOnly to, int? fieldId, int? typeId);
}

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _db;

    public ReportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AdminDashboardViewModel> GetDashboardAsync(bool staffOnly)
    {
        var today = AppClock.Today;
        var now = AppClock.Now;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var previousStart = monthStart.AddMonths(-1);
        var todayStart = today.ToDateTime(TimeOnly.MinValue);
        var tomorrow = todayStart.AddDays(1);
        var time = TimeOnly.FromDateTime(now);

        var customerRole = await _db.Roles.Where(r => r.Name == "Customer").Select(r => r.Id).FirstAsync();
        var customerIds = _db.UserRoles.Where(r => r.RoleId == customerRole).Select(r => r.UserId);

        var paid = _db.Payments.Where(p => p.PaymentStatus == PaymentStatus.Paid && p.PaymentDate != null);
        var totalRevenue = await paid.SumAsync(p => (decimal?)p.Amount) ?? 0;
        var thisMonthRevenue = await paid.Where(p => p.PaymentDate >= monthStart && p.PaymentDate < monthStart.AddMonths(1)).SumAsync(p => (decimal?)p.Amount) ?? 0;
        var lastMonthRevenue = await paid.Where(p => p.PaymentDate >= previousStart && p.PaymentDate < monthStart).SumAsync(p => (decimal?)p.Amount) ?? 0;
        var todayRevenue = await paid.Where(p => p.PaymentDate >= todayStart && p.PaymentDate < tomorrow).SumAsync(p => (decimal?)p.Amount) ?? 0;

        var shiftStartHour = now.Hour < 15 ? 6 : 15;
        var shiftEndHour = now.Hour < 15 ? 15 : 23;
        var shiftStart = today.ToDateTime(new TimeOnly(shiftStartHour, 0));
        var shiftEnd = today.ToDateTime(new TimeOnly(shiftEndHour, 0));
        var shiftRevenue = await paid.Where(p => p.PaymentDate >= shiftStart && p.PaymentDate < shiftEnd).SumAsync(p => (decimal?)p.Amount) ?? 0;

        var activeFields = await _db.FootballFields.CountAsync(f => f.IsActive && f.Status != FieldStatus.Inactive);
        var openHours = 17;

        var monthBookings = await _db.Bookings.Where(b => b.BookingDate >= DateOnly.FromDateTime(monthStart) && b.BookingDate <= today).ToListAsync();
        var bookedHours = monthBookings.Where(b => b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.NoShow)
            .Sum(b => (decimal)(b.EndTime - b.StartTime).TotalHours);
        var capacity = activeFields * openHours * Math.Max(1, today.Day);
        var fill = capacity == 0 ? 0 : Math.Round(bookedHours / capacity * 100m, 1);

        var busyFieldIds = await _db.Bookings.Where(b =>
                b.BookingDate == today && b.StartTime <= time && b.EndTime > time
                && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Playing))
            .Select(b => b.FieldId).Distinct().CountAsync();

        var maintenanceFields = await _db.FootballFields.CountAsync(f => f.IsActive && f.Status == FieldStatus.Maintenance);
        var model = new AdminDashboardViewModel
        {
            IsStaffDashboard = staffOnly,
            TotalRevenue = totalRevenue,
            RevenueChangePercent = lastMonthRevenue == 0 ? 100 : Math.Round((thisMonthRevenue - lastMonthRevenue) / lastMonthRevenue * 100m, 1),
            TodayRevenue = todayRevenue,
            ShiftRevenue = shiftRevenue,
            TodayBookings = await _db.Bookings.CountAsync(b => b.BookingDate == today && b.Status != BookingStatus.Cancelled),
            MonthBookings = await _db.Bookings.CountAsync(b => b.BookingDate >= DateOnly.FromDateTime(monthStart) && b.BookingDate <= today && b.Status != BookingStatus.Cancelled),
            TotalCustomers = await _db.Users.CountAsync(u => customerIds.Contains(u.Id)),
            NewCustomers = await _db.Users.CountAsync(u => customerIds.Contains(u.Id) && u.CreatedAt >= monthStart),
            TotalFields = activeFields,
            FieldsInUse = busyFieldIds,
            FreeFields = Math.Max(0, activeFields - busyFieldIds - maintenanceFields),
            CancelledBookings = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.Cancelled && b.BookingDate >= DateOnly.FromDateTime(monthStart)),
            PendingBookings = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.Pending && b.BookingDate >= today),
            CheckedInCustomers = await _db.Bookings.CountAsync(b => b.BookingDate == today && (b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Playing)),
            FillRate = fill
        };

        for (var i = 6; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            var from = day.ToDateTime(TimeOnly.MinValue);
            var to = from.AddDays(1);
            model.Last7Labels.Add(day.ToString("dd/MM"));
            model.Last7Revenue.Add(await paid.Where(p => p.PaymentDate >= from && p.PaymentDate < to).SumAsync(p => (decimal?)p.Amount) ?? 0);
        }

        for (var i = 11; i >= 0; i--)
        {
            var start = monthStart.AddMonths(-i);
            var end = start.AddMonths(1);
            model.MonthLabels.Add(start.ToString("MM/yyyy"));
            model.MonthRevenue.Add(await paid.Where(p => p.PaymentDate >= start && p.PaymentDate < end).SumAsync(p => (decimal?)p.Amount) ?? 0);
        }

        var statusGroups = await _db.Bookings.GroupBy(b => b.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        foreach (var status in Enum.GetValues<BookingStatus>())
        {
            model.StatusLabels.Add(status.Text());
            model.StatusCounts.Add(statusGroups.FirstOrDefault(s => s.Key == status)?.Count ?? 0);
        }

        var fieldStats = await _db.Bookings
            .Where(b => b.Status != BookingStatus.Cancelled)
            .GroupBy(b => b.Field!.Name)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
        model.FieldLabels = fieldStats.Select(x => x.Name).ToList();
        model.FieldBookingCounts = fieldStats.Select(x => x.Count).ToList();
        model.TopFieldLabels = fieldStats.Take(5).Select(x => x.Name).ToList();
        model.TopFieldCounts = fieldStats.Take(5).Select(x => x.Count).ToList();

        model.TodaySchedule = await _db.Bookings
            .Include(b => b.Field).Include(b => b.Customer)
            .Where(b => b.BookingDate == today)
            .OrderBy(b => b.StartTime)
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
                PaymentStatus = b.PaymentStatus
            }).ToListAsync();

        model.RecentBookings = await _db.Bookings
            .Include(b => b.Field).Include(b => b.Customer)
            .OrderByDescending(b => b.CreatedAt)
            .Take(8)
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

        return model;
    }

    public async Task<ReportViewModel> GetReportAsync(DateOnly from, DateOnly to, int? fieldId, int? typeId, string focus, int page)
    {
        if (to < from)
        {
            to = from;
        }

        var bookings = await Filtered(from, to, fieldId, typeId)
            .Include(b => b.Field).ThenInclude(f => f!.FieldType)
            .Include(b => b.Customer)
            .Include(b => b.ServiceLines).ThenInclude(s => s.Service)
            .OrderByDescending(b => b.BookingDate).ThenBy(b => b.StartTime)
            .ToListAsync();

        var active = bookings.Where(b => b.Status != BookingStatus.Cancelled).ToList();
        var paidAmount = await _db.Payments.Where(p =>
                p.PaymentStatus == PaymentStatus.Paid && p.PaymentDate != null
                && p.PaymentDate >= from.ToDateTime(TimeOnly.MinValue)
                && p.PaymentDate < to.AddDays(1).ToDateTime(TimeOnly.MinValue)
                && bookings.Select(b => b.Id).Contains(p.BookingId))
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        var fieldCount = fieldId != null ? 1 : await _db.FootballFields.CountAsync(f => f.IsActive && (typeId == null || f.FieldTypeId == typeId));
        var days = to.DayNumber - from.DayNumber + 1;
        var hours = active.Sum(b => (decimal)(b.EndTime - b.StartTime).TotalHours);
        var capacity = Math.Max(1, fieldCount) * 17 * Math.Max(1, days);
        var pageSize = AppConstants.PageSize;
        page = page < 1 ? 1 : page;

        var model = new ReportViewModel
        {
            From = from,
            To = to,
            FieldId = fieldId,
            FieldTypeId = typeId,
            Focus = focus,
            TotalBookings = bookings.Count,
            CompletedBookings = bookings.Count(b => b.Status == BookingStatus.Completed),
            CancelledBookings = bookings.Count(b => b.Status == BookingStatus.Cancelled),
            Revenue = paidAmount,
            AverageBookingValue = active.Count == 0 ? 0 : Math.Round(active.Average(b => b.TotalAmount), 0),
            CancelRate = bookings.Count == 0 ? 0 : Math.Round(bookings.Count(b => b.Status == BookingStatus.Cancelled) * 100m / bookings.Count, 1),
            FillRate = Math.Round(hours / capacity * 100m, 1),
            Fields = await _db.FootballFields.OrderBy(f => f.FieldCode).ToListAsync(),
            Types = await _db.FieldTypes.OrderBy(t => t.Capacity).ToListAsync(),
            Paging = new PagingModel
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = bookings.Count,
                TotalPages = Math.Max(1, (int)Math.Ceiling(bookings.Count / (double)pageSize))
            }
        };

        var customerRole = await _db.Roles.Where(r => r.Name == "Customer").Select(r => r.Id).FirstAsync();
        var customerIds = _db.UserRoles.Where(r => r.RoleId == customerRole).Select(r => r.UserId);
        model.NewCustomers = await _db.Users.CountAsync(u =>
            customerIds.Contains(u.Id) && u.CreatedAt >= from.ToDateTime(TimeOnly.MinValue) && u.CreatedAt < to.AddDays(1).ToDateTime(TimeOnly.MinValue));

        model.PopularFields = active.GroupBy(b => b.Field?.Name ?? "Sân")
            .Select(g => new NamedStat { Name = g.Key, Count = g.Count(), Amount = g.Sum(x => x.TotalAmount) })
            .OrderByDescending(x => x.Count).Take(10).ToList();
        model.PopularHours = active.GroupBy(b => b.StartTime.Hour)
            .Select(g => new NamedStat { Name = $"{g.Key:00}:00", Count = g.Count(), Amount = g.Sum(x => x.TotalAmount) })
            .OrderByDescending(x => x.Count).ToList();
        model.PopularServices = active.SelectMany(b => b.ServiceLines)
            .GroupBy(s => s.Service?.Name ?? "Dịch vụ")
            .Select(g => new NamedStat { Name = g.Key, Count = g.Sum(x => x.Quantity), Amount = g.Sum(x => x.TotalPrice) })
            .OrderByDescending(x => x.Count).ToList();
        model.TopCustomers = active.GroupBy(b => b.Customer?.FullName ?? "Khách")
            .Select(g => new NamedStat { Name = g.Key, Count = g.Count(), Amount = g.Sum(x => x.TotalAmount) })
            .OrderByDescending(x => x.Amount).Take(8).ToList();

        var cursor = from;
        while (cursor <= to && model.RevenueLabels.Count < 31)
        {
            model.RevenueLabels.Add(cursor.ToString("dd/MM"));
            model.RevenueSeries.Add(active.Where(b => b.BookingDate == cursor && b.PaymentStatus == PaymentStatus.Paid).Sum(b => b.TotalAmount));
            cursor = cursor.AddDays(1);
        }

        model.Bookings = bookings.Skip((page - 1) * pageSize).Take(pageSize).Select(Map).ToList();
        return model;
    }

    public async Task<List<BookingListItemViewModel>> GetReportBookingsAsync(DateOnly from, DateOnly to, int? fieldId, int? typeId)
    {
        var bookings = await Filtered(from, to, fieldId, typeId)
            .Include(b => b.Field).Include(b => b.Customer)
            .OrderBy(b => b.BookingDate).ToListAsync();
        return bookings.Select(Map).ToList();
    }

    private IQueryable<Booking> Filtered(DateOnly from, DateOnly to, int? fieldId, int? typeId)
    {
        var query = _db.Bookings.Where(b => b.BookingDate >= from && b.BookingDate <= to);
        if (fieldId != null)
        {
            query = query.Where(b => b.FieldId == fieldId);
        }

        if (typeId != null)
        {
            query = query.Where(b => b.Field!.FieldTypeId == typeId);
        }

        return query;
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
        PaymentStatus = b.PaymentStatus,
        CreatedAt = b.CreatedAt
    };
}
