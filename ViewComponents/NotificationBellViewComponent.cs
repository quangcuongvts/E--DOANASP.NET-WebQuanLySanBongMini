using Microsoft.AspNetCore.Identity;
using MiniFootballBooking.Services;

namespace MiniFootballBooking.ViewComponents;

public class NotificationBellViewComponent : ViewComponent
{
    private readonly Data.ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly INotificationService _notifications;

    public NotificationBellViewComponent(Data.ApplicationDbContext db, UserManager<ApplicationUser> users, INotificationService notifications)
    {
        _db = db;
        _users = users;
        _notifications = notifications;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Content(string.Empty);
        }

        var user = await _users.GetUserAsync(HttpContext.User);
        if (user == null)
        {
            return Content(string.Empty);
        }

        if (User.IsInRole("Customer"))
        {
            await _notifications.EnsureUpcomingAsync(user.Id);
        }

        var items = await _db.Notifications.Where(n => n.UserId == user.Id)
            .OrderByDescending(n => n.CreatedAt).Take(5)
            .Select(n => new NotificationItemViewModel
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                Link = n.Link
            }).ToListAsync();
        return View(new NotificationBellViewModel
        {
            Unread = await _db.Notifications.CountAsync(n => n.UserId == user.Id && !n.IsRead),
            Items = items
        });
    }
}
