namespace MiniFootballBooking.Models;

public class FieldImage
{
    public int Id { get; set; }
    public int FieldId { get; set; }
    public FootballField? Field { get; set; }

    [Required]
    [StringLength(300)]
    public string ImageUrl { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
}
