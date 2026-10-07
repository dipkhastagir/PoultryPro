namespace PoultryPro.Models.DTO
{
    public class DashboardSummary
    {
        public int TotalBirds { get; set; }
        public int ActiveBatches { get; set; }
        public int TotalFarms { get; set; }
        public int StaffOnDuty { get; set; }
        public int OverdueVaccinations { get; set; }
        public int LowStockCount { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public decimal MonthlyExpenses { get; set; }
        public decimal NetProfit => MonthlyRevenue - MonthlyExpenses;
        public decimal ProfitMargin => MonthlyRevenue > 0 ? NetProfit / MonthlyRevenue * 100 : 0;
    }
}