using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace PoultryPro.Infrastructure;

/// <summary>Builds RFC-4180 CSV downloads from any sequence of objects or Dapper rows.</summary>
public static class Csv
{
    public static FileContentResult File<T>(IEnumerable<T> rows, string fileName)
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Build(rows))).ToArray();
        return new FileContentResult(bytes, "text/csv") { FileDownloadName = fileName };
    }

    public static string Build<T>(IEnumerable<T> rows)
    {
        var list = rows.ToList();
        var sb = new StringBuilder();
        if (list.Count == 0) return "";

        // Dapper dynamic rows implement IDictionary<string, object>
        if (list[0] is IDictionary<string, object> first)
        {
            var cols = first.Keys.ToList();
            sb.AppendLine(string.Join(",", cols.Select(Escape)));
            foreach (IDictionary<string, object> r in list.Cast<IDictionary<string, object>>())
                sb.AppendLine(string.Join(",", cols.Select(c => Escape(Format(r.TryGetValue(c, out var v) ? v : null)))));
            return sb.ToString();
        }

        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && IsSimple(p.PropertyType)).ToArray();
        sb.AppendLine(string.Join(",", props.Select(p => Escape(p.Name))));
        foreach (var r in list)
            sb.AppendLine(string.Join(",", props.Select(p => Escape(Format(p.GetValue(r))))));
        return sb.ToString();
    }

    private static bool IsSimple(Type t)
    {
        t = Nullable.GetUnderlyingType(t) ?? t;
        return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(TimeSpan) || t == typeof(Guid);
    }

    private static string Format(object? v) => v switch
    {
        null => "",
        DateTime d => d.TimeOfDay == TimeSpan.Zero ? d.ToString("yyyy-MM-dd") : d.ToString("yyyy-MM-dd HH:mm:ss"),
        decimal m => m.ToString(CultureInfo.InvariantCulture),
        double db => db.ToString(CultureInfo.InvariantCulture),
        float fl => fl.ToString(CultureInfo.InvariantCulture),
        bool b => b ? "1" : "0",
        _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? ""
    };

    private static string Escape(string s)
        => s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
