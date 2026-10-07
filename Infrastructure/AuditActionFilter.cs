using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PoultryPro.Infrastructure;

/// <summary>
/// Global filter: every successful POST (create / update / delete / workflow action) is written to the
/// AuditLogs table with user, controller, action, entity id and client IP. Passwords are never logged.
/// </summary>
public class AuditActionFilter(IAuditService audit) : IAsyncActionFilter
{
    private static readonly string[] SkipControllers = { "Account", "Setup", "Api", "SensorApi", "MobileApi" };
    private static readonly string[] SensitiveKeys = { "password", "newpassword", "currentpassword", "confirmpassword", "__requestverificationtoken" };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        var req = context.HttpContext.Request;
        if (!HttpMethods.IsPost(req.Method) || executed.Exception != null) return;

        var controller = context.RouteData.Values["controller"]?.ToString() ?? "";
        var action = context.RouteData.Values["action"]?.ToString() ?? "";
        if (SkipControllers.Contains(controller, StringComparer.OrdinalIgnoreCase)) return;

        string? id = context.RouteData.Values["id"]?.ToString();
        var details = new List<string>();
        if (req.HasFormContentType && !controller.Equals("Settings", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var kv in req.Form)
            {
                var key = kv.Key.ToLowerInvariant();
                if (SensitiveKeys.Contains(key) || key.Contains("password") || key.Contains("apikey") || key.Contains("secret") || key.Contains("synckey") || key.EndsWith("key")) continue;
                if (id == null && (key == "id" || key.EndsWith(".id"))) id = kv.Value.ToString();
                if (details.Count < 8 && kv.Value.ToString().Length < 60) details.Add($"{kv.Key}={kv.Value}");
            }
        }
        try
        {
            await audit.LogAsync(context.HttpContext.User.UserName(), action, controller, id,
                string.Join("; ", details), context.HttpContext.Connection.RemoteIpAddress?.ToString());
        }
        catch { /* auditing must never break the request */ }
    }
}
