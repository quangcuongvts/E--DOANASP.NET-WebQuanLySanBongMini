using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniFootballBooking.Services;

namespace MiniFootballBooking.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Manager,Staff")]
public class DashboardController : Controller
{
    private readonly IReportService _reports;

    public DashboardController(IReportService reports)
    {
        _reports = reports;
    }

    public async Task<IActionResult> Index()
    {
        var staffOnly = User.IsInRole("Staff") && !User.IsInRole("Admin") && !User.IsInRole("Manager");
        var model = await _reports.GetDashboardAsync(staffOnly);
        return View(model);
    }
}
