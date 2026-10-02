namespace Slotik.Services;

public class LiqPaySettings
{
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string ServerUrl { get; set; } = string.Empty;
    public string ResultUrl { get; set; } = string.Empty;
    public decimal BasicPriceUah { get; set; }
    public decimal ProPriceUah { get; set; }
}
