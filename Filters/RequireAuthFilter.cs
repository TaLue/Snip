using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Snip.Filters;

public class RequireAuthFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.HttpContext.Session.GetString("auth") != "1")
            context.Result = new RedirectToActionResult("Login", "Home", null);
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
