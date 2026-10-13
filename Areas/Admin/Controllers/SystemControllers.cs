using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MiniFootballBooking.Services;

namespace MiniFootballBooking.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly Data.ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public UsersController(UserManager<ApplicationUser> users, Data.ApplicationDbContext db, IAuditService audit)
    {
        _users = users;
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? q, string? role, string? status, int page = 1)
    {
        var users = _users.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            users = users.Where(u => u.FullName.Contains(q) || (u.Email != null && u.Email.Contains(q)) || (u.PhoneNumber != null && u.PhoneNumber.Contains(q)));
        }

        if (status == "active")
        {
            users = users.Where(u => u.IsActive);
        }
        else if (status == "locked")
        {
            users = users.Where(u => !u.IsActive);
        }

        var list = await users.OrderByDescending(u => u.CreatedAt).ToListAsync();
        var rows = new List<UserRowViewModel>();
        foreach (var user in list)
        {
            var roles = await _users.GetRolesAsync(user);
            var roleName = roles.FirstOrDefault() ?? "Customer";
            if (!string.IsNullOrWhiteSpace(role) && roleName != role)
            {
                continue;
            }

            rows.Add(new UserRowViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? "",
                Phone = user.PhoneNumber ?? "",
                AvatarUrl = user.AvatarUrl,
                Role = roleName,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            });
        }

        page = page < 1 ? 1 : page;
        return View(new UserListViewModel
        {
            Items = rows.Skip((page - 1) * AppConstants.PageSize).Take(AppConstants.PageSize).ToList(),
            Keyword = q,
            Role = role,
            Status = status,
            Paging = new PagingModel
            {
                Page = page,
                TotalItems = rows.Count,
                TotalPages = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)AppConstants.PageSize))
            }
        });
    }

    public async Task<IActionResult> Details(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        ViewBag.Roles = await _users.GetRolesAsync(user);
        ViewBag.Bookings = await _db.Bookings.CountAsync(b => b.CustomerId == id);
        return View(user);
    }

    public IActionResult Create() => View(new UserFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "Vui lòng nhập mật khẩu.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            EmailConfirmed = true,
            FullName = model.FullName.Trim(),
            PhoneNumber = model.Phone,
            DateOfBirth = model.DateOfBirth,
            Gender = model.Gender,
            Address = model.Address,
            IsActive = model.IsActive,
            LockoutEnabled = true,
            CreatedAt = AppClock.Now
        };
        var result = await _users.CreateAsync(user, model.Password!);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await _users.AddToRoleAsync(user, model.Role);
        if (!model.IsActive)
        {
            await _users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        }

        var actor = await _users.GetUserAsync(User);
        await _audit.LogAsync(actor?.Id, "CREATE USER", "User", user.Id, null, new { user.Email, model.Role });
        TempData["Success"] = "Đã tạo tài khoản.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var roles = await _users.GetRolesAsync(user);
        return View(new UserFormViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? "",
            Phone = user.PhoneNumber ?? "",
            DateOfBirth = user.DateOfBirth,
            Gender = user.Gender,
            Address = user.Address,
            Role = roles.FirstOrDefault() ?? "Customer",
            IsActive = user.IsActive,
            AvatarUrl = user.AvatarUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserFormViewModel model)
    {
        var user = await _users.FindByIdAsync(model.Id!);
        if (user == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var oldRole = (await _users.GetRolesAsync(user)).FirstOrDefault();
        if (oldRole == "Admin" && model.Role != "Admin" && await CountAdmins() <= 1)
        {
            ModelState.AddModelError(nameof(model.Role), "Không thể bỏ vai trò của quản trị viên cuối cùng.");
            return View(model);
        }

        user.FullName = model.FullName.Trim();
        user.Email = model.Email.Trim();
        user.UserName = model.Email.Trim();
        user.PhoneNumber = model.Phone;
        user.DateOfBirth = model.DateOfBirth;
        user.Gender = model.Gender;
        user.Address = model.Address;
        user.IsActive = model.IsActive;
        user.UpdatedAt = AppClock.Now;
        var updated = await _users.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            foreach (var error in updated.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        var roles = await _users.GetRolesAsync(user);
        await _users.RemoveFromRolesAsync(user, roles);
        await _users.AddToRoleAsync(user, model.Role);
        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var reset = await _users.ResetPasswordAsync(user, token, model.Password);
            if (!reset.Succeeded)
            {
                TempData["Error"] = string.Join(" ", reset.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Edit), new { id = user.Id });
            }
        }

        await _users.SetLockoutEndDateAsync(user, model.IsActive ? null : DateTimeOffset.MaxValue);
        var actor = await _users.GetUserAsync(User);
        await _audit.LogAsync(actor?.Id, oldRole == model.Role ? "UPDATE USER" : "CHANGE ROLE", "User", user.Id, new { Role = oldRole }, new { Role = model.Role, user.Email });
        TempData["Success"] = "Đã cập nhật người dùng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lock(string id, bool unlock)
    {
        var user = await _users.FindByIdAsync(id);
        var actor = await _users.GetUserAsync(User);
        if (user == null || actor == null)
        {
            return NotFound();
        }

        if (user.Id == actor.Id)
        {
            TempData["Error"] = "Không thể khóa chính tài khoản đang đăng nhập.";
            return RedirectToAction(nameof(Index));
        }

        if (!unlock && await _users.IsInRoleAsync(user, "Admin") && await CountAdmins(activeOnly: true) <= 1)
        {
            TempData["Error"] = "Không thể khóa quản trị viên cuối cùng.";
            return RedirectToAction(nameof(Index));
        }

        user.IsActive = unlock;
        user.UpdatedAt = AppClock.Now;
        await _users.UpdateAsync(user);
        await _users.SetLockoutEndDateAsync(user, unlock ? null : DateTimeOffset.MaxValue);
        if (unlock)
        {
            await _users.ResetAccessFailedCountAsync(user);
        }

        await _audit.LogAsync(actor.Id, unlock ? "UNLOCK USER" : "LOCK USER", "User", user.Id, new { IsActive = !unlock }, new { user.IsActive });
        TempData["Success"] = unlock ? "Đã mở khóa tài khoản." : "Đã khóa tài khoản.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<int> CountAdmins(bool activeOnly = false)
    {
        var admins = await _users.GetUsersInRoleAsync("Admin");
        return activeOnly ? admins.Count(a => a.IsActive) : admins.Count;
    }
}

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class RolesController : Controller
{
    private readonly RoleManager<IdentityRole> _roles;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditService _audit;

    public RolesController(RoleManager<IdentityRole> roles, UserManager<ApplicationUser> users, IAuditService audit)
    {
        _roles = roles;
        _users = users;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var model = new RolePageViewModel();
        foreach (var role in new[] { "Admin", "Manager", "Staff", "Customer" })
        {
            var members = await _users.GetUsersInRoleAsync(role);
            model.Roles.Add(new RoleStatViewModel { Name = role, Count = members.Count });
        }

        model.Users = await _users.Users.OrderBy(u => u.FullName).Select(u => new UserRowViewModel
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email ?? ""
        }).ToListAsync();
        foreach (var row in model.Users)
        {
            var user = await _users.FindByIdAsync(row.Id);
            row.Role = (await _users.GetRolesAsync(user!)).FirstOrDefault() ?? "";
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(string userId, string role)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user == null || !new[] { "Admin", "Manager", "Staff", "Customer" }.Contains(role))
        {
            return NotFound();
        }

        var old = (await _users.GetRolesAsync(user)).FirstOrDefault();
        if (old == "Admin" && role != "Admin")
        {
            var admins = await _users.GetUsersInRoleAsync("Admin");
            if (admins.Count(a => a.IsActive) <= 1)
            {
                TempData["Error"] = "Không thể đổi vai trò của quản trị viên cuối cùng.";
                return RedirectToAction(nameof(Index));
            }
        }

        if (old != null)
        {
            await _users.RemoveFromRolesAsync(user, await _users.GetRolesAsync(user));
        }

        await _users.AddToRoleAsync(user, role);
        var actor = await _users.GetUserAsync(User);
        await _audit.LogAsync(actor?.Id, "CHANGE ROLE", "User", user.Id, new { Role = old }, new { Role = role });
        TempData["Success"] = $"Đã gán vai trò {role} cho {user.FullName}.";
        return RedirectToAction(nameof(Index));
    }
}

[Area("Admin")]
[Authorize(Roles = "Admin,Manager")]
public class ReviewsController : Controller
{
    private readonly Data.ApplicationDbContext _db;

    public ReviewsController(Data.ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? q, int? rating, int page = 1)
    {
        var query = _db.Reviews.Include(r => r.Customer).Include(r => r.Field).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(r => r.Comment.Contains(q) || r.Customer!.FullName.Contains(q) || r.Field!.Name.Contains(q));
        }

        if (rating != null)
        {
            query = query.Where(r => r.Rating == rating);
        }

        page = page < 1 ? 1 : page;
        var total = await query.CountAsync();
        ViewBag.Paging = new PagingModel { Page = page, TotalItems = total, TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)AppConstants.PageSize)) };
        ViewBag.Keyword = q;
        ViewBag.Rating = rating;
        return View(await query.OrderByDescending(r => r.CreatedAt).Skip((page - 1) * AppConstants.PageSize).Take(AppConstants.PageSize).ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Hide(int id, bool visible)
    {
        var review = await _db.Reviews.FindAsync(id);
        if (review == null)
        {
            return NotFound();
        }

        review.IsVisible = visible;
        await _db.SaveChangesAsync();
        var ratings = await _db.Reviews.Where(r => r.FieldId == review.FieldId && r.IsVisible).Select(r => r.Rating).ToListAsync();
        var field = await _db.FootballFields.FirstAsync(f => f.Id == review.FieldId);
        field.RatingAverage = ratings.Count == 0 ? 0 : Math.Round((decimal)ratings.Average(), 1);
        await _db.SaveChangesAsync();
        TempData["Success"] = visible ? "Đã hiện đánh giá." : "Đã ẩn đánh giá vi phạm.";
        return RedirectToAction(nameof(Index));
    }
}

[Area("Admin")]
[Authorize(Roles = "Admin,Manager")]
public class MaintenancesController : Controller
{
    private readonly Data.ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public MaintenancesController(Data.ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IActionResult> Index(MaintenanceStatus? status, int page = 1)
    {
        var query = _db.Maintenances.Include(m => m.Field).Include(m => m.Creator).AsQueryable();
        if (status != null)
        {
            query = query.Where(m => m.Status == status);
        }

        page = page < 1 ? 1 : page;
        var total = await query.CountAsync();
        ViewBag.Paging = new PagingModel { Page = page, TotalItems = total, TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)AppConstants.PageSize)) };
        ViewBag.Status = status;
        return View(await query.OrderByDescending(m => m.StartDateTime).Skip((page - 1) * AppConstants.PageSize).Take(AppConstants.PageSize).ToListAsync());
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Fields = await _db.FootballFields.OrderBy(f => f.FieldCode).ToListAsync();
        return View(new Maintenance { StartDateTime = AppClock.Now, EndDateTime = AppClock.Now.AddHours(4), Status = MaintenanceStatus.Scheduled });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Maintenance model)
    {
        if (model.EndDateTime <= model.StartDateTime)
        {
            ModelState.AddModelError(nameof(model.EndDateTime), "Thời gian kết thúc phải sau thời gian bắt đầu.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Fields = await _db.FootballFields.OrderBy(f => f.FieldCode).ToListAsync();
            return View(model);
        }

        var actor = await _users.GetUserAsync(User);
        model.CreatedBy = actor?.Id;
        model.CreatedAt = AppClock.Now;
        _db.Maintenances.Add(model);
        await SyncField(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã tạo lịch bảo trì. Sân không nhận đặt trong khoảng này.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await _db.Maintenances.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }

        ViewBag.Fields = await _db.FootballFields.OrderBy(f => f.FieldCode).ToListAsync();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Maintenance model)
    {
        var item = await _db.Maintenances.FindAsync(model.Id);
        if (item == null)
        {
            return NotFound();
        }

        if (model.EndDateTime <= model.StartDateTime)
        {
            ModelState.AddModelError(nameof(model.EndDateTime), "Thời gian kết thúc phải sau thời gian bắt đầu.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Fields = await _db.FootballFields.OrderBy(f => f.FieldCode).ToListAsync();
            return View(model);
        }

        item.FieldId = model.FieldId;
        item.Title = model.Title;
        item.Description = model.Description;
        item.StartDateTime = model.StartDateTime;
        item.EndDateTime = model.EndDateTime;
        item.Cost = model.Cost;
        item.Status = model.Status;
        await SyncField(item);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật bảo trì.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.Maintenances.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }

        item.Status = MaintenanceStatus.Cancelled;
        var field = await _db.FootballFields.FindAsync(item.FieldId);
        if (field != null && field.Status == FieldStatus.Maintenance)
        {
            var still = await _db.Maintenances.AnyAsync(m => m.Id != item.Id && m.FieldId == field.Id && m.Status == MaintenanceStatus.InProgress);
            if (!still)
            {
                field.Status = FieldStatus.Available;
            }
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã hủy lịch bảo trì.";
        return RedirectToAction(nameof(Index));
    }

    private async Task SyncField(Maintenance model)
    {
        var field = await _db.FootballFields.FindAsync(model.FieldId);
        if (field == null)
        {
            return;
        }

        if (model.Status == MaintenanceStatus.InProgress)
        {
            field.Status = FieldStatus.Maintenance;
        }
        else if (model.Status is MaintenanceStatus.Completed or MaintenanceStatus.Cancelled && field.Status == FieldStatus.Maintenance)
        {
            field.Status = FieldStatus.Available;
        }
    }
}
