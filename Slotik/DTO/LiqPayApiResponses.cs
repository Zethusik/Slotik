using System.Text.Json;
using System.Text.Json.Serialization;

namespace Slotik.DTO;

public sealed class LiqPayRefundResponse
{
    [JsonPropertyName("result")]
    public string? Result { get; init; }

    [JsonPropertyName("action")]
    public string? Action { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("payment_id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? PaymentId { get; init; }

    [JsonPropertyName("wait_amount")]
    [JsonConverter(typeof(FlexibleNullableBooleanConverter))]
    public bool? WaitAmount { get; init; }

    [JsonPropertyName("err_code")]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("err_description")]
    public string? ErrorDescription { get; init; }

    [JsonIgnore]
    public bool IsSuccess =>
        string.Equals(Result, "ok", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsReversed =>
        string.Equals(Status, "reversed", StringComparison.OrdinalIgnoreCase);
}

public sealed class LiqPayPaymentStatusResponse
{
    [JsonPropertyName("public_key")]
    public string? PublicKey { get; init; }
    [JsonPropertyName("result")]
    public string? Result { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("payment_id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? PaymentId { get; init; }

    [JsonPropertyName("order_id")]
    public string? OrderId { get; init; }

    [JsonPropertyName("amount")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? Amount { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    [JsonPropertyName("err_code")]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("err_description")]
    public string? ErrorDescription { get; init; }

    [JsonIgnore]
    public bool IsSuccess =>
        string.Equals(Result, "ok", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsReversed =>
        string.Equals(Status, "reversed", StringComparison.OrdinalIgnoreCase);
}

public sealed class FlexibleNullableBooleanConverter : JsonConverter<bool?>
{
    public override bool? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Null => null,
            JsonTokenType.String when bool.TryParse(
                reader.GetString(),
                out var value) => value,

            JsonTokenType.Number when reader.TryGetInt32(
                out var number) => number != 0,

            _ => throw new JsonException(
                "Invalid boolean value returned by LiqPay.")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        bool? value,
        JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteBooleanValue(value.Value);
        else
            writer.WriteNullValue();
    }
}
