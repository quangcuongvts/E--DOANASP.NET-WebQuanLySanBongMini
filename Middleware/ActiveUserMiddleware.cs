using Microsoft.AspNetCore.Identity;

namespace MiniFootballBooking.Middleware;

public class ActiveUserMiddleware
{
    private readonly RequestDelegate _next;

    public ActiveUserMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var isAuthPage = path.StartsWith("/Account/Login", StringComparison.OrdinalIgnoreCase)
                         || path.StartsWith("/Account/Logout", StringComparison.OrdinalIgnoreCase)
                         || path.StartsWith("/Account/Register", StringComparison.OrdinalIgnoreCase)
                         || path.StartsWith("/Account/AccessDenied", StringComparison.OrdinalIgnoreCase)
                         || path.StartsWith("/Account/ForgotPassword", StringComparison.OrdinalIgnoreCase)
                         || path.StartsWith("/Account/ResetPassword", StringComparison.OrdinalIgnoreCase);

        if (context.User.Identity?.IsAuthenticated == true && !isAuthPage)
        {
            var user = await userManager.GetUserAsync(context.User);
            if (user == null || !user.IsActive || (user.LockoutEnd.HasValue && user.LockoutEnd.Value.UtcDateTime > DateTime.UtcNow))
            {
                await signInManager.SignOutAsync();
                context.Response.Redirect("/Account/Login?locked=1");
                return;
            }
        }

        await _next(context);
    }
}
