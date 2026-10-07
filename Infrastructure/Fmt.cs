using System.Globalization;

namespace PoultryPro.Infrastructure;

/// <summary>Formatting helpers used by views (currency, numbers, relative time, badges).</summary>
public static class Fmt
{
    public const string Currency = "৳";

    public static string Money(decimal value, int decimals = 0)
        => (value < 0 ? "-" : "") + Currency + Math.Abs(value).ToString("N" + decimals, CultureInfo.InvariantCulture);

    public static string Money(decimal? value, int decimals = 0) => value.HasValue ? Money(value.Value, decimals) : "—";

    /// <summary>Compact money: ৳1.2M, ৳45.3K</summary>
    public static string MoneyShort(decimal value)
    {
        var abs = Math.Abs(value);
        var sign = value < 0 ? "-" : "";
        if (abs >= 10_000_000) return $"{sign}{Currency}{abs / 10_000_000m:0.##} Cr";
        if (abs >= 100_000)    return $"{sign}{Currency}{abs / 100_000m:0.##} Lakh";
        if (abs >= 1_000)      return $"{sign}{Currency}{abs / 1_000m:0.#}K";
        return $"{sign}{Currency}{abs:0}";
    }

    public static string Num(decimal value, int decimals = 0) => value.ToString("N" + decimals, CultureInfo.InvariantCulture);
    public static string Num(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
    public static string Pct(decimal value, int decimals = 1) => value.ToString("N" + decimals, CultureInfo.InvariantCulture) + "%";

    public static string Date(DateTime? d) => d.HasValue ? d.Value.ToString("dd MMM yyyy") : "—";
    public static string DateTime(DateTime? d) => d.HasValue ? d.Value.ToString("dd MMM yyyy, hh:mm tt") : "—";

    public static string Ago(DateTime? d)
    {
        if (!d.HasValue) return "never";
        var span = System.DateTime.Now - d.Value;
        if (span.TotalSeconds < 60) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
        if (span.TotalHours < 24)   return $"{(int)span.TotalHours} h ago";
        if (span.TotalDays < 30)    return $"{(int)span.TotalDays} d ago";
        return d.Value.ToString("dd MMM yyyy");
    }

    /// <summary>Maps any common status word to one of the badge CSS classes defined in site.css.</summary>
    public static string Badge(string? status) => (status ?? "").Trim().ToLowerInvariant() switch
    {
        "healthy" or "valid" or "done" or "completed" or "paid" or "delivered" or "received" or "synced"
            or "in stock" or "present" or "resolved" or "active" or "on duty" or "a" or "passed" or "in production" => "badge-healthy",
        "ready for sale" or "confirmed" or "dispatched" or "running" or "in progress" or "ordered" or "harvest ready" => "badge-ready",
        "pending" or "planned" or "draft" or "partial" or "late" or "half day" or "expiring soon" or "low stock" or "warning"
            or "vaccination due" or "under observation" or "under treatment" or "medium" or "high" or "b" or "c" or "on leave" or "open" => "badge-warning-soft",
        "overdue" or "expired" or "cancelled" or "unpaid" or "absent" or "critical" or "out of stock" or "failed"
            or "sick" or "outbreak" or "urgent" or "d" or "abandoned" or "off duty" or "inactive" => "badge-danger-soft",
        "info" or "harvested" or "closed" or "leave" or "low" or "planned trial" => "badge-info-soft",
        _ => "badge-gray"
    };

    public static string SeverityIcon(string? severity) => severity switch
    {
        "Critical" => "bi-exclamation-octagon-fill text-danger",
        "Warning"  => "bi-exclamation-triangle-fill text-warning",
        _          => "bi-info-circle-fill text-info"
    };
}
