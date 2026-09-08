using System.Collections.Generic;

namespace Slotik.DTO
{
    public class AdminStatsDto
    {
        public int MastersTotal { get; set; }
        public int ActiveSubscriptions { get; set; }
        public decimal MonthlyIncome { get; set; }
        public int BookingsTotal { get; set; }
    }

    public class FinanceStatsDto
    {
        public decimal MonthlyIncome { get; set; }
        public double MonthlyIncomeChange { get; set; }
        public int NewMasters { get; set; }
        public double NewMastersChange { get; set; }
        public int NewClients { get; set; }
        public double NewClientsChange { get; set; }
        public int PaidSubscriptions { get; set; }
        public List<DailyRevenueDto> DailyRevenue { get; set; } = new();
    }

    public class DailyRevenueDto
    {
        public int Day { get; set; }
        public decimal Amount { get; set; }
    }

    public class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public int MastersCount { get; set; }
    }

    public class CreateCategoryDto
    {
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
}