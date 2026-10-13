namespace MiniFootballBooking.Models;

public class Post
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề")]
    [StringLength(180)]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Slug { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tóm tắt")]
    [StringLength(300)]
    [Display(Name = "Tóm tắt")]
    public string Summary { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập nội dung")]
    [Display(Name = "Nội dung")]
    public string Content { get; set; } = string.Empty;

    [StringLength(300)]
    public string? ThumbnailUrl { get; set; }

    public string? AuthorId { get; set; }
    public ApplicationUser? Author { get; set; }

    public DateTime? PublishedAt { get; set; }
    public bool IsPublished { get; set; }
}
