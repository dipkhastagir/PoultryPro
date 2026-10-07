using PoultryPro.Models.Entities;

namespace PoultryPro.Infrastructure;

/// <summary>
/// Poultry production science used by the analytics engine, alert rules and research module.
/// All formulas are documented so they can be cited in a methods section.
/// </summary>
public static class PoultryMath
{
    /// <summary>
    /// Temperature-Humidity Index (NRC 1971 formulation, widely used for livestock heat stress):
    /// THI = (1.8·T + 32) − (0.55 − 0.0055·RH)·(1.8·T − 26),  T in °C, RH in %.
    /// </summary>
    public static decimal THI(decimal tempC, decimal rhPct)
    {
        var t = (double)tempC; var rh = (double)rhPct;
        return (decimal)Math.Round((1.8 * t + 32) - (0.55 - 0.0055 * rh) * (1.8 * t - 26), 1);
    }

    /// <summary>Heat-stress band for a THI value, using configurable warning / critical thresholds.</summary>
    public static string HeatBand(decimal thi, decimal warning = 80, decimal critical = 85)
        => thi >= critical + 5 ? "Emergency" : thi >= critical ? "Danger" : thi >= warning ? "Alert" : "Normal";

    /// <summary>European Production Efficiency Factor = Livability% × LiveWeight(kg) × 100 / (Age × FCR).</summary>
    public static decimal EPEF(decimal livabilityPct, decimal liveWeightKg, int ageDays, decimal fcr)
        => ageDays > 0 && fcr > 0 ? Math.Round(livabilityPct * liveWeightKg * 100 / (ageDays * fcr), 1) : 0;

    /// <summary>Feed Conversion Ratio = feed consumed (kg) / live mass produced (kg).</summary>
    public static decimal FCR(decimal feedKg, decimal liveMassKg) => liveMassKg > 0 ? Math.Round(feedKg / liveMassKg, 3) : 0;

    /// <summary>Linear interpolation of a breed-standard curve at a given age (days).</summary>
    public static (decimal WeightGm, decimal DailyFeedGm, decimal Fcr, decimal CumMortPct)? Standard(IEnumerable<BreedStandard> curve, int age)
    {
        var pts = curve.OrderBy(s => s.AgeDay).ToList();
        if (pts.Count == 0) return null;
        var lo = pts.LastOrDefault(p => p.AgeDay <= age) ?? pts[0];
        var hi = pts.FirstOrDefault(p => p.AgeDay > age);
        if (hi == null || hi.AgeDay == lo.AgeDay)
            return (lo.TargetWeightGm, lo.DailyFeedGm, lo.TargetFCR, lo.TargetCumMortPct);
        var f = (decimal)(age - lo.AgeDay) / (hi.AgeDay - lo.AgeDay);
        return (lo.TargetWeightGm + (hi.TargetWeightGm - lo.TargetWeightGm) * f,
                lo.DailyFeedGm + (hi.DailyFeedGm - lo.DailyFeedGm) * f,
                lo.TargetFCR + (hi.TargetFCR - lo.TargetFCR) * f,
                lo.TargetCumMortPct + (hi.TargetCumMortPct - lo.TargetCumMortPct) * f);
    }

    /// <summary>Ordinary least squares y = a + b·x. Returns intercept, slope and R².</summary>
    public static (double Intercept, double Slope, double R2) LinearRegression(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        int n = Math.Min(x.Count, y.Count);
        if (n < 2) return (n == 1 ? y[0] : 0, 0, 0);
        double mx = x.Take(n).Average(), my = y.Take(n).Average();
        double sxy = 0, sxx = 0, syy = 0;
        for (int i = 0; i < n; i++) { sxy += (x[i] - mx) * (y[i] - my); sxx += (x[i] - mx) * (x[i] - mx); syy += (y[i] - my) * (y[i] - my); }
        if (sxx == 0) return (my, 0, 0);
        var b = sxy / sxx; var a = my - b * mx;
        var r2 = syy == 0 ? 1 : (sxy * sxy) / (sxx * syy);
        return (a, b, r2);
    }

    /// <summary>
    /// Exponentially-weighted moving average control chart (EWMA, λ = 0.3) with an L·σ limit.
    /// Returns, for each observation, the EWMA value, upper control limit and whether it is out of control.
    /// </summary>
    public static List<(double Value, double Ewma, double Ucl, bool Signal)> Ewma(IReadOnlyList<double> series, double lambda = 0.3, double L = 3.0, int baseline = 7)
    {
        var result = new List<(double, double, double, bool)>();
        if (series.Count == 0) return result;
        var basePts = series.Take(Math.Max(2, Math.Min(baseline, series.Count))).ToList();
        double mu = basePts.Average();
        double sd = Math.Sqrt(basePts.Sum(v => (v - mu) * (v - mu)) / Math.Max(1, basePts.Count - 1));
        if (sd < 1e-9) sd = Math.Max(0.5, mu * 0.25);
        double z = mu;
        for (int i = 0; i < series.Count; i++)
        {
            z = lambda * series[i] + (1 - lambda) * z;
            var factor = Math.Sqrt(lambda / (2 - lambda) * (1 - Math.Pow(1 - lambda, 2 * (i + 1))));
            var ucl = mu + L * sd * factor;
            result.Add((series[i], Math.Round(z, 3), Math.Round(ucl, 3), z > ucl));
        }
        return result;
    }

    /// <summary>Robust z-score of the last value against the previous window (median / MAD).</summary>
    public static double RobustZ(IReadOnlyList<double> window, double value)
    {
        if (window.Count < 3) return 0;
        var sorted = window.OrderBy(v => v).ToList();
        double med = sorted[sorted.Count / 2];
        var dev = window.Select(v => Math.Abs(v - med)).OrderBy(v => v).ToList();
        double mad = dev[dev.Count / 2] * 1.4826;
        if (mad < 1e-9) mad = Math.Max(0.5, med * 0.25);
        return (value - med) / mad;
    }

    /// <summary>Pearson correlation coefficient.</summary>
    public static double Pearson(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        int n = Math.Min(x.Count, y.Count);
        if (n < 3) return 0;
        double mx = x.Take(n).Average(), my = y.Take(n).Average(), sxy = 0, sxx = 0, syy = 0;
        for (int i = 0; i < n; i++) { sxy += (x[i] - mx) * (y[i] - my); sxx += (x[i] - mx) * (x[i] - mx); syy += (y[i] - my) * (y[i] - my); }
        return sxx == 0 || syy == 0 ? 0 : sxy / Math.Sqrt(sxx * syy);
    }

    /// <summary>Welch's t statistic for two independent samples (unequal variances).</summary>
    public static (double T, double Df) WelchT(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        if (a.Count < 2 || b.Count < 2) return (0, 0);
        double ma = a.Average(), mb = b.Average();
        double va = a.Sum(v => (v - ma) * (v - ma)) / (a.Count - 1);
        double vb = b.Sum(v => (v - mb) * (v - mb)) / (b.Count - 1);
        double se2 = va / a.Count + vb / b.Count;
        if (se2 <= 0) return (0, a.Count + b.Count - 2);
        double t = (ma - mb) / Math.Sqrt(se2);
        double df = se2 * se2 / ((va * va) / (a.Count * a.Count * (a.Count - 1)) + (vb * vb) / (b.Count * b.Count * (b.Count - 1)));
        return (t, df);
    }

    /// <summary>
    /// Great-circle distance in metres between two WGS-84 points (haversine formula, mean Earth radius 6 371 008.8 m):
    /// d = 2R·asin(√(sin²(Δφ/2) + cos φ1·cos φ2·sin²(Δλ/2))).
    /// </summary>
    public static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6_371_008.8;
        static double Rad(double deg) => deg * Math.PI / 180.0;
        double dLat = Rad(lat2 - lat1), dLon = Rad(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
    }

    /// <summary>Two-sided p-value for a t statistic (Student t CDF via regularized incomplete beta).</summary>
    public static double PValueTwoSided(double t, double df)
    {
        if (df <= 0) return 1;
        double x = df / (df + t * t);
        return Math.Min(1, Math.Max(0, IncompleteBeta(df / 2, 0.5, x)));
    }

    private static double IncompleteBeta(double a, double b, double x)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;
        double lbeta = LogGamma(a + b) - LogGamma(a) - LogGamma(b) + a * Math.Log(x) + b * Math.Log(1 - x);
        if (x < (a + 1) / (a + b + 2)) return Math.Exp(lbeta) * BetaCf(a, b, x) / a;
        return 1 - Math.Exp(lbeta) * BetaCf(b, a, 1 - x) / b;
    }

    private static double BetaCf(double a, double b, double x)
    {
        const int maxIt = 200; const double eps = 3e-12, fpmin = 1e-300;
        double qab = a + b, qap = a + 1, qam = a - 1, c = 1, d = 1 - qab * x / qap;
        if (Math.Abs(d) < fpmin) d = fpmin;
        d = 1 / d; double h = d;
        for (int m = 1; m <= maxIt; m++)
        {
            int m2 = 2 * m;
            double aa = m * (b - m) * x / ((qam + m2) * (a + m2));
            d = 1 + aa * d; if (Math.Abs(d) < fpmin) d = fpmin;
            c = 1 + aa / c; if (Math.Abs(c) < fpmin) c = fpmin;
            d = 1 / d; h *= d * c;
            aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
            d = 1 + aa * d; if (Math.Abs(d) < fpmin) d = fpmin;
            c = 1 + aa / c; if (Math.Abs(c) < fpmin) c = fpmin;
            d = 1 / d; double del = d * c; h *= del;
            if (Math.Abs(del - 1) < eps) break;
        }
        return h;
    }

    private static double LogGamma(double x)
    {
        double[] c = { 76.18009172947146, -86.50532032941677, 24.01409824083091, -1.231739572450155, 0.1208650973866179e-2, -0.5395239384953e-5 };
        double y = x, tmp = x + 5.5;
        tmp -= (x + 0.5) * Math.Log(tmp);
        double ser = 1.000000000190015;
        for (int j = 0; j < 6; j++) ser += c[j] / ++y;
        return -tmp + Math.Log(2.5066282746310005 * ser / x);
    }
}
