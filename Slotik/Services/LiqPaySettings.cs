namespace Slotik.Services;

public class LiqPaySettings
{
    public bool? Enabled { get; set; }
    public bool IsEnabled => Enabled ?? (!string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey));
    public int MaxReconciliationAttempts { get; set; } = 6;
    public int ReconciliationIntervalSeconds { get; set; } = 60;
    public int ReconciliationMaxAgeDays { get; set; } = 30;
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string ServerUrl { get; set; } = string.Empty;
    public string ResultUrl { get; set; } = string.Empty;
    public decimal BasicPriceUah { get; set; }
    public decimal ProPriceUah { get; set; }
}
