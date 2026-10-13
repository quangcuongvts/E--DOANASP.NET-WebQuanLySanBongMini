using System.ComponentModel.DataAnnotations.Schema;

namespace MiniFootballBooking.Models;

[Table("Contacts")]
public class ContactMessage
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    [StringLength(100)]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [RegularExpression(@"^0[0-9]{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ")]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề")]
    [StringLength(150)]
    [Display(Name = "Tiêu đề")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập nội dung")]
    [StringLength(2000)]
    [Display(Name = "Nội dung")]
    public string Message { get; set; } = string.Empty;

    public ContactStatus Status { get; set; } = ContactStatus.New;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
