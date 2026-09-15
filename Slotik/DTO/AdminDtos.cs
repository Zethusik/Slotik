using Slotik.Models;
using Slotik.Models.Enums;

namespace Slotik.DTO;

public class AdminStatsDto
{
    public int MastersTotal { get; set; }
    public int ClientsTotal { get; set; }
    public int BookingsTotal { get; set; }
    public decimal RevenueTotal { get; set; }
    public int ActiveSubscriptions { get; set; }
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

public class MasterAdminDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? SubscriptionUntil { get; set; }
    public string Tariff { get; set; } = string.Empty;
    public bool IsBlocked { get; set; }
    public string AvatarUrl { get; set; } = string.Empty;
    public string DistrictName {  get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
public class FullMasterDto
{ 
    public int Id { get; set; }
    public string? FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; } = string.Empty;
    public string? Category { get; set; } = string.Empty;
    public string? City { get; set; } = string.Empty;
    public string? Status { get; set; } = string.Empty;
    public string? SubscriptionUntil { get; set; }
    public string? Tariff { get; set; } = string.Empty;
    public bool? IsBlocked { get; set; }
    public string? AvatarUrl { get; set; } = string.Empty;
    public string? DistrictName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public string? slug {  get; set; } = string.Empty;
    public string? Email { get; set; } = string.Empty;
    public string? Phone { get; set; } = string.Empty ;

    public int? TariffPrice { get; set; }

    public string? Currency { get; set; } = "UAH";
    public string? BillingPeriod { get; set; } = "month";

    public string? nextPaymentAt { get; set; }
    public int BookingsCount { get; set; }
    public List<Payment> Payments { get; set; } = new List<Payment>();

}

public class UpdateSubscriptionDto
{
    public SubscriptionPlan Plan { get; set; }
    public int Days { get; set; } = 30;
}