namespace PoultryPro.Models.ViewModels
{
    /// <summary>One day of layer production, aggregated over the selected flock(s).</summary>
    public class EggDailyPoint
    {
        public DateTime Date { get; set; }
        public int Hens { get; set; }
        public int Eggs { get; set; }
        public int GradeA { get; set; }
        public int GradeB { get; set; }
        public int Cracked { get; set; }
        public int Dirty { get; set; }
        public decimal FeedKg { get; set; }
        /// <summary>Hen-weighted mean flock age in weeks.</summary>
        public decimal AgeWeeks { get; set; }
        public decimal? AvgEggWeightGm { get; set; }

        public decimal HenDayPct => Hens > 0 ? Math.Round(Eggs * 100m / Hens, 2) : 0;
        public decimal CrackedPct => Eggs > 0 ? Math.Round(Cracked * 100m / Eggs, 2) : 0;
        /// <summary>Grams of feed per dozen eggs.</summary>
        public decimal FeedPerDozenGm => Eggs > 0 && FeedKg > 0 ? Math.Round(FeedKg * 1000m / (Eggs / 12m), 0) : 0;
        public decimal? ReferenceHenDayPct => LayerReference.HenDayPct(AgeWeeks);
    }

    /// <summary>Simplified commercial brown-layer reference curve (Hy-Line / Lohmann Brown type) for hen-day production.</summary>
    public static class LayerReference
    {
        private static readonly (decimal Week, decimal Pct)[] Curve =
        {
            (18, 5), (19, 30), (20, 60), (21, 80), (22, 89), (23, 92), (24, 94), (26, 95), (30, 95),
            (35, 94), (40, 92.5m), (45, 91), (50, 89), (55, 87), (60, 85), (65, 82.5m), (70, 80), (80, 75)
        };

        /// <summary>Expected hen-day % at a given age in weeks; null before 18 weeks (not yet in lay).</summary>
        public static decimal? HenDayPct(decimal ageWeeks)
        {
            if (ageWeeks < Curve[0].Week) return null;
            if (ageWeeks >= Curve[^1].Week) return Curve[^1].Pct;
            for (var i = 1; i < Curve.Length; i++)
            {
                if (ageWeeks <= Curve[i].Week)
                {
                    var (w0, p0) = Curve[i - 1];
                    var (w1, p1) = Curve[i];
                    return Math.Round(p0 + (p1 - p0) * (ageWeeks - w0) / (w1 - w0), 1);
                }
            }
            return null;
        }

        public const decimal PeakPct = 95m;
    }
}
