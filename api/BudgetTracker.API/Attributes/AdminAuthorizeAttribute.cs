using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Linq;

namespace BudgetTracker.API.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AdminAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var isAdminClaim = user.Claims.FirstOrDefault(c => c.Type == "IsAdmin");
        if (isAdminClaim == null || !bool.TryParse(isAdminClaim.Value, out var isAdmin) || !isAdmin)
        {
            context.Result = new ForbidResult();
            return;
        }
    }
}

