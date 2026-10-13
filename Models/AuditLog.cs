namespace MiniFootballBooking.Models;

public class AuditLog
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [StringLength(80)]
    public string Action { get; set; } = string.Empty;

    [StringLength(80)]
    public string EntityName { get; set; } = string.Empty;

    [StringLength(50)]
    public string? EntityId { get; set; }

    public string? OldValues { get; set; }
    public string? NewValues { get; set; }

    [StringLength(60)]
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
