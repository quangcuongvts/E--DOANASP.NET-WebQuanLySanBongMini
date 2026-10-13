using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace MiniFootballBooking.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly Data.ApplicationDbContext _db;
    private readonly ImageStorage _images;

    public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, Data.ApplicationDbContext db, ImageStorage images)
    {
        _users = users;
        _signIn = signIn;
        _db = db;
        _images = images;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null, int? locked = null)
    {
        if (locked == 1)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản đã bị khóa hoặc phiên đăng nhập không còn hiệu lực.");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _users.FindByEmailAsync(model.Email);
        if (user == null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng, hoặc tài khoản đã bị khóa.");
            return View(model);
        }

        var result = await _signIn.PasswordSignInAsync(user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            if (await _users.IsInRoleAsync(user, "Admin") || await _users.IsInRoleAsync(user, "Manager") || await _users.IsInRoleAsync(user, "Staff"))
            {
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            }

            return RedirectToAction(nameof(Dashboard));
        }

        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "Tài khoản tạm khóa do đăng nhập sai nhiều lần."
            : "Email hoặc mật khẩu không đúng.");
        return View(model);
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName.Trim(),
            PhoneNumber = model.Phone,
            IsActive = true,
            LockoutEnabled = true,
            CreatedAt = AppClock.Now
        };
        var result = await _users.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await _users.AddToRoleAsync(user, "Customer");
        await _signIn.SignInAsync(user, isPersistent: false);
        TempData["Success"] = "Đăng ký thành công. Chào mừng bạn đến Mini Football.";
        return RedirectToAction(nameof(Dashboard));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _users.FindByEmailAsync(model.Email);
        if (user == null || !user.IsActive)
        {
            TempData["Success"] = "Nếu email tồn tại, hướng dẫn đặt lại mật khẩu sẽ được hiển thị.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = Url.Action(nameof(ResetPassword), "Account", new { userId = user.Id, token = encoded }, Request.Scheme);
        TempData["ResetLink"] = link;
        return View("ForgotPasswordConfirmation");
    }

    [HttpGet]
    public IActionResult ResetPassword(string userId, string token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordViewModel { UserId = userId, Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _users.FindByIdAsync(model.UserId);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Không tìm thấy tài khoản.");
            return View(model);
        }

        var token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token));
        var result = await _users.ResetPasswordAsync(user, token, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        TempData["Success"] = "Đặt lại mật khẩu thành công. Bạn có thể đăng nhập.";
        return RedirectToAction(nameof(Login));
    }

    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Dashboard()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var today = AppClock.Today;
        var bookings = _db.Bookings.Include(b => b.Field).Where(b => b.CustomerId == user.Id);
        var next = await bookings
            .Where(b => b.BookingDate >= today && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.Completed && b.Status != BookingStatus.NoShow)
            .OrderBy(b => b.BookingDate).ThenBy(b => b.StartTime)
            .FirstOrDefaultAsync();

        var model = new CustomerDashboardViewModel
        {
            FullName = user.FullName,
            Upcoming = await bookings.CountAsync(b => b.BookingDate >= today && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.Completed && b.Status != BookingStatus.NoShow),
            Completed = await bookings.CountAsync(b => b.Status == BookingStatus.Completed),
            Cancelled = await bookings.CountAsync(b => b.Status == BookingStatus.Cancelled),
            TotalSpent = await bookings.Where(b => b.PaymentStatus == PaymentStatus.Paid).SumAsync(b => (decimal?)b.TotalAmount) ?? 0,
            NextMatch = next == null ? null : ToItem(next),
            Recent = await bookings.OrderByDescending(b => b.CreatedAt).Take(5).Select(b => new BookingListItemViewModel
            {
                Id = b.Id,
                BookingCode = b.BookingCode,
                FieldName = b.Field!.Name,
                BookingDate = b.BookingDate,
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                TotalAmount = b.TotalAmount,
                Status = b.Status,
                PaymentStatus = b.PaymentStatus
            }).ToListAsync()
        };
        return View(model);
    }

    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        return View(MapProfile(user));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(CustomerProfileViewModel model, IFormFile? avatar)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            model.AvatarUrl = user.AvatarUrl;
            return View(model);
        }

        if (await _users.Users.AnyAsync(u => u.Email == model.Email && u.Id != user.Id))
        {
            ModelState.AddModelError(nameof(model.Email), "Email đã được sử dụng.");
            return View(model);
        }

        if (avatar != null)
        {
            var saved = await _images.SaveAsync(avatar, "avatars");
            if (!saved.Ok)
            {
                ModelState.AddModelError(string.Empty, saved.Error);
                return View(model);
            }

            user.AvatarUrl = saved.Path;
        }

        user.FullName = model.FullName.Trim();
        user.Email = model.Email.Trim();
        user.UserName = model.Email.Trim();
        user.PhoneNumber = model.Phone;
        user.DateOfBirth = model.DateOfBirth;
        user.Gender = model.Gender;
        user.Address = model.Address;
        user.UpdatedAt = AppClock.Now;
        var result = await _users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await _signIn.RefreshSignInAsync(user);
        TempData["Success"] = "Đã cập nhật hồ sơ.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _users.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var result = await _users.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await _signIn.RefreshSignInAsync(user);
        TempData["Success"] = "Đổi mật khẩu thành công.";
        return RedirectToAction(nameof(Profile));
    }

    public IActionResult AccessDenied() => View();

    private static CustomerProfileViewModel MapProfile(ApplicationUser user) => new()
    {
        FullName = user.FullName,
        Email = user.Email ?? "",
        Phone = user.PhoneNumber ?? "",
        DateOfBirth = user.DateOfBirth,
        Gender = user.Gender,
        Address = user.Address,
        AvatarUrl = user.AvatarUrl
    };

    private static BookingListItemViewModel ToItem(Booking booking) => new()
    {
        Id = booking.Id,
        BookingCode = booking.BookingCode,
        FieldName = booking.Field?.Name ?? "",
        BookingDate = booking.BookingDate,
        StartTime = booking.StartTime,
        EndTime = booking.EndTime,
        TotalAmount = booking.TotalAmount,
        Status = booking.Status,
        PaymentStatus = booking.PaymentStatus
    };
}
