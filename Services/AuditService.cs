using System.Text.Json;
using MiniFootballBooking.Data;

namespace MiniFootballBooking.Services;

public interface IAuditService
{
    Task LogAsync(string? userId, string action, string entityName, string? entityId, object? oldValues, object? newValues);
}

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(ApplicationDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task LogAsync(string? userId, string action, string entityName, string? entityId, object? oldValues, object? newValues)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValues = oldValues == null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues == null ? null : JsonSerializer.Serialize(newValues),
            IpAddress = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            CreatedAt = AppClock.Now
        });
        await _db.SaveChangesAsync();
    }
}
