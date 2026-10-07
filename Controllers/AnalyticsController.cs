using Microsoft.AspNetCore.Mvc;

namespace PoultryPro.Controllers;

/// <summary>Precision-production analytics (read-only, open to every signed-in role).</summary>
public class AnalyticsController(IAnalyticsService analytics) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Analytics Hub";
        ViewData["Subtitle"] = "Production science, early warning and forecasting across all flocks";
        return View(await analytics.GetHubAsync());
    }

    public async Task<IActionResult> GrowthCurve(int? batchId)
    {
        ViewData["Title"] = "Growth vs Breed Standard";
        ViewData["Subtitle"] = "Daily body weight and cumulative FCR compared with the interpolated breed curve";
        return View(await analytics.GetGrowthCurveAsync(batchId));
    }

    public async Task<IActionResult> Efficiency()
    {
        ViewData["Title"] = "Production Efficiency (EPEF)";
        ViewData["Subtitle"] = "European Production Efficiency Factor, FCR, livability and daily gain ranking";
        return View(await analytics.GetEfficiencyAsync());
    }

    public async Task<IActionResult> Anomalies(int? batchId)
    {
        ViewData["Title"] = "Mortality Anomaly Detection";
        ViewData["Subtitle"] = "EWMA control chart and robust z-scores on daily mortality";
        return View(await analytics.GetAnomaliesAsync(batchId));
    }

    public async Task<IActionResult> HeatStress()
    {
        ViewData["Title"] = "Heat Stress (THI)";
        ViewData["Subtitle"] = "Temperature-Humidity Index from IoT sensors and daily flock records";
        return View(await analytics.GetHeatStressAsync());
    }

    public async Task<IActionResult> RiskScore()
    {
        ViewData["Title"] = "Flock Risk Score";
        ViewData["Subtitle"] = "Transparent weighted 0–100 risk index for every active flock";
        return View(await analytics.GetRiskScoresAsync());
    }

    public async Task<IActionResult> Forecast(int? batchId)
    {
        ViewData["Title"] = "Harvest Forecast";
        ViewData["Subtitle"] = "Predicted market-weight date, live mass and revenue for meat flocks";
        return View(await analytics.GetForecastAsync(batchId));
    }

    public async Task<IActionResult> FeedForecast()
    {
        ViewData["Title"] = "Feed Demand Forecast";
        ViewData["Subtitle"] = "Next 14 days of feed demand vs stock on hand";
        return View(await analytics.GetFeedForecastAsync());
    }

    public async Task<IActionResult> Benchmark()
    {
        ViewData["Title"] = "Farm Benchmarking";
        ViewData["Subtitle"] = "Compare farms on FCR, EPEF, mortality and biosecurity";
        return View(await analytics.GetBenchmarkAsync());
    }

    public async Task<IActionResult> Carbon()
    {
        ViewData["Title"] = "Carbon Footprint";
        ViewData["Subtitle"] = "Indicative greenhouse-gas emissions per flock and per kg live weight";
        return View(await analytics.GetCarbonAsync());
    }
}
