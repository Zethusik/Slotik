using System.Text.Json.Serialization;

namespace Slotik.DTO;

public class LiqPayCallbackDto
{
    [JsonPropertyName("public_key")]
    public string PublicKey { get; set; } = string.Empty;

    [JsonPropertyName("order_id")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("payment_id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? PaymentId { get; set; }

    [JsonPropertyName("amount")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public class LiqPayCheckoutResponse
{
    public string CheckoutUrl { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
}
