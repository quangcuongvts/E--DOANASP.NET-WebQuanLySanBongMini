namespace MiniFootballBooking.Models;

public class SystemSetting
{
    public int Id { get; set; }

    [Required]
    [StringLength(80)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Value { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Description { get; set; }
}
