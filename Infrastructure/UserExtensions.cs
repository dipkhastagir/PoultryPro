using System.Security.Claims;

namespace PoultryPro.Infrastructure;

public static class UserExtensions
{
    public static int UserId(this ClaimsPrincipal u)
        => int.TryParse(u.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public static string UserName(this ClaimsPrincipal u) => u.Identity?.Name ?? "anonymous";
    public static string FullName(this ClaimsPrincipal u) => u.FindFirstValue("FullName") ?? u.UserName();
    public static string Role(this ClaimsPrincipal u) => u.FindFirstValue(ClaimTypes.Role) ?? Roles.Viewer;

    public static int? FieldOfficerId(this ClaimsPrincipal u)
        => int.TryParse(u.FindFirstValue("FieldOfficerId"), out var id) ? id : null;

    public static bool IsAny(this ClaimsPrincipal u, string commaSeparatedRoles)
        => commaSeparatedRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(u.IsInRole);

    public static string Initials(this ClaimsPrincipal u)
    {
        var parts = u.FullName().Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => char.IsLetter(p[0])).Take(2);
        var s = string.Concat(parts.Select(p => char.ToUpperInvariant(p[0])));
        return s.Length == 0 ? "?" : s;
    }
}
