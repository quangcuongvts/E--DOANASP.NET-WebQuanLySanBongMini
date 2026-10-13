using Microsoft.AspNetCore.Identity;
using MiniFootballBooking.Data;

namespace MiniFootballBooking.Services;

public interface INotificationService
{
    Task NotifyAsync(string userId, string title, string message, NotificationType type, string? link);
    Task NotifyRolesAsync(IEnumerable<string> roles, string title, string message, NotificationType type, string? link);
    Task EnsureUpcomingAsync(string userId);
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public NotificationService(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task NotifyAsync(string userId, string title, string message, NotificationType type, string? link)
    {
        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            Link = link,
            CreatedAt = AppClock.Now
        });
        await _db.SaveChangesAsync();
    }

    public async Task NotifyRolesAsync(IEnumerable<string> roles, string title, string message, NotificationType type, string? link)
    {
        var ids = new HashSet<string>();
        foreach (var role in roles)
        {
            var users = await _users.GetUsersInRoleAsync(role);
            foreach (var user in users)
            {
                ids.Add(user.Id);
            }
        }

        foreach (var id in ids)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = id,
                Title = title,
                Message = message,
                Type = type,
                Link = link,
                CreatedAt = AppClock.Now
            });
        }

        if (ids.Count > 0)
        {
            await _db.SaveChangesAsync();
        }
    }

    public async Task EnsureUpcomingAsync(string userId)
    {
        var today = AppClock.Today;
        var horizon = AppClock.Now.AddHours(24);
        var bookings = await _db.Bookings.Include(b => b.Field)
            .Where(b => b.CustomerId == userId
                        && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Pending)
                        && b.BookingDate >= today
                        && b.BookingDate <= today.AddDays(1))
            .ToListAsync();

        var changed = false;
        foreach (var booking in bookings)
        {
            var start = booking.BookingDate.ToDateTime(booking.StartTime);
            if (start <= AppClock.Now || start > horizon)
            {
                continue;
            }

            var link = $"/Booking/Details/{booking.Id}";
            var exists = await _db.Notifications.AnyAsync(n =>
                n.UserId == userId && n.Type == NotificationType.UpcomingMatch && n.Link == link);
            if (exists)
            {
                continue;
            }

            _db.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = "Sắp đến giờ đá",
                Message = $"{booking.Field?.Name} lúc {booking.StartTime:HH:mm} ngày {booking.BookingDate:dd/MM/yyyy}.",
                Type = NotificationType.UpcomingMatch,
                Link = link,
                CreatedAt = AppClock.Now
            });
            changed = true;
        }

        if (changed)
        {
            await _db.SaveChangesAsync();
        }
    }
}
