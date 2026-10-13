using Microsoft.AspNetCore.Identity;

namespace MiniFootballBooking.Models;

public class ApplicationUser : IdentityUser
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Ngày sinh")]
    public DateOnly? DateOfBirth { get; set; }

    [Display(Name = "Giới tính")]
    public Gender Gender { get; set; } = Gender.Other;

    [StringLength(250, ErrorMessage = "Địa chỉ tối đa 250 ký tự")]
    [Display(Name = "Địa chỉ")]
    public string? Address { get; set; }

    [StringLength(300)]
    [Display(Name = "Ảnh đại diện")]
    public string? AvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
