using DentalLab.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
namespace DentalLab.Web.Middleware;
public sealed class TemporaryPasswordMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> users)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var path=context.Request.Path.Value??"";
            var allowed=path.StartsWith("/Account/Manage/ChangePassword",StringComparison.OrdinalIgnoreCase)||path.StartsWith("/Account/Logout",StringComparison.OrdinalIgnoreCase)||path.StartsWith("/_",StringComparison.OrdinalIgnoreCase)||path.Contains('.');
            if(!allowed)
            {
                var user=await users.GetUserAsync(context.User);
                if(user?.MustChangePassword==true){context.Response.Redirect("/Account/Manage/ChangePassword");return;}
            }
        }
        await next(context);
    }
}
