using PoultryPro.Models.Entities;

namespace PoultryPro.Infrastructure;

/// <summary>Result of the field-visit geofence check and Visit Trust Score for one visit.</summary>
public sealed class VisitTrustResult
{
    public const string Verified = "Verified";
    public const string OffSite  = "Off-site";
    public const string NoGps    = "No GPS";

    /// <summary>Great-circle distance from the check-in fix to the farm coordinates (m); null when either side has no GPS.</summary>
    public double? DistanceM { get; init; }
    public decimal RadiusM { get; init; }
    /// <summary>Effective tolerance = radius + reported GPS accuracy (m).</summary>
    public decimal ToleranceM { get; init; }
    public string GpsStatus { get; init; } = NoGps;
    /// <summary>Why the status is "No GPS" (missing check-in fix vs farm not geo-tagged); null otherwise.</summary>
    public string? GpsNote { get; init; }

    public bool GpsVerified => GpsStatus == Verified;
    public bool HasPhoto { get; init; }
    public bool HasSignature { get; init; }
    public bool HasDailyRecord { get; init; }
    public bool IsSynced { get; init; }

    public int Score { get; init; }

    public string GpsBadge => GpsStatus switch { Verified => "badge-healthy", OffSite => "badge-danger-soft", _ => "badge-gray" };
    public string GpsIcon  => GpsStatus switch { Verified => "bi-geo-alt-fill", OffSite => "bi-exclamation-triangle-fill", _ => "bi-geo" };
    public string ScoreBand => Score >= 80 ? "High" : Score >= 50 ? "Medium" : "Low";
    public string ScoreBadge => Score >= 80 ? "badge-healthy" : Score >= 50 ? "badge-warning-soft" : "badge-danger-soft";

    /// <summary>The five score components in display order: (label, points available, earned?).</summary>
    public IReadOnlyList<(string Label, int Points, bool Earned)> Components =>
    [
        ("GPS check-in verified inside geofence", VisitTrust.WeightGps,       GpsVerified),
        ("At least one visit photo",              VisitTrust.WeightPhoto,     HasPhoto),
        ("Farmer signature captured",             VisitTrust.WeightSignature, HasSignature),
        ("At least one daily flock record",       VisitTrust.WeightRecord,    HasDailyRecord),
        ("Synced from the mobile app",            VisitTrust.WeightSync,      IsSynced)
    ];
}

/// <summary>
/// Single source of truth for field-visit geofence verification and the Visit Trust Score (0–100).
/// <para>GPS status: <c>Verified</c> when haversine(check-in, farm) ≤ radius + GPS accuracy; <c>Off-site</c> otherwise;
/// <c>No GPS</c> when the check-in fix or the farm coordinates are missing.</para>
/// <para>Trust = 40·[GPS verified] + 20·[≥1 photo] + 20·[farmer signature] + 10·[≥1 daily record] + 10·[synced].</para>
/// </summary>
public static class VisitTrust
{
    public const string SettingKey = "Visit.GeofenceRadiusM";
    public const decimal DefaultRadiusM = 300;

    public const int WeightGps = 40, WeightPhoto = 20, WeightSignature = 20, WeightRecord = 10, WeightSync = 10;

    public const string Formula =
        "Trust = 40·GPS✓ + 20·Photo✓ + 20·Signature✓ + 10·DailyRecord✓ + 10·Synced✓   (GPS✓ ⇔ haversine(check-in, farm) ≤ radius + accuracy)";

    public static VisitTrustResult Evaluate(FarmVisit v, decimal radiusM)
    {
        if (radiusM <= 0) radiusM = DefaultRadiusM;
        var accuracy = Math.Max(0, v.GpsAccuracyMeters ?? 0);
        var tolerance = radiusM + accuracy;

        double? distance = null;
        string? note = null;
        if (!(v.CheckInLatitude.HasValue && v.CheckInLongitude.HasValue)) note = "No check-in GPS fix recorded";
        else if (!(v.FarmLatitude.HasValue && v.FarmLongitude.HasValue)) note = "Farm has no coordinates on file";
        else
            distance = PoultryMath.HaversineMeters(
                (double)v.CheckInLatitude.Value, (double)v.CheckInLongitude.Value,
                (double)v.FarmLatitude.Value, (double)v.FarmLongitude.Value);

        var status = distance is null ? VisitTrustResult.NoGps
                   : distance.Value <= (double)tolerance ? VisitTrustResult.Verified
                   : VisitTrustResult.OffSite;

        bool photo = v.PhotoCount > 0, sig = v.SignatureCount > 0, rec = v.DailyRecordCount > 0;
        bool synced = string.Equals(v.SyncStatus, "Synced", StringComparison.OrdinalIgnoreCase);

        var score = (status == VisitTrustResult.Verified ? WeightGps : 0)
                  + (photo ? WeightPhoto : 0) + (sig ? WeightSignature : 0)
                  + (rec ? WeightRecord : 0) + (synced ? WeightSync : 0);

        return new VisitTrustResult
        {
            DistanceM = distance, RadiusM = radiusM, ToleranceM = tolerance, GpsStatus = status, GpsNote = note,
            HasPhoto = photo, HasSignature = sig, HasDailyRecord = rec, IsSynced = synced, Score = score
        };
    }

    /// <summary>Evaluates every visit and stores the result on <see cref="FarmVisit.Trust"/>; returns the same list.</summary>
    public static List<FarmVisit> Apply(IEnumerable<FarmVisit> visits, decimal radiusM)
    {
        var list = visits as List<FarmVisit> ?? visits.ToList();
        foreach (var v in list) v.Trust = Evaluate(v, radiusM);
        return list;
    }

    /// <summary>Officer-level summary: mean trust score and % of visits geo-verified (null when there are no visits).</summary>
    public static (double? AvgScore, double? GeoVerifiedPct) Summarise(IEnumerable<FarmVisit> visits, decimal radiusM)
    {
        var results = visits.Select(v => v.Trust ?? Evaluate(v, radiusM)).ToList();
        if (results.Count == 0) return (null, null);
        return (results.Average(r => r.Score), results.Count(r => r.GpsVerified) * 100.0 / results.Count);
    }
}
