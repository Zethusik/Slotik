using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Crypto.Digests;
using Slotik.DTO;
using Slotik.Models.Enums;

namespace Slotik.Services;

public class LiqPayService
{
    public const string CheckoutUrl =
        "https://www.liqpay.ua/api/3/checkout";

    private const string ApiRequestPath = "api/request";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly LiqPaySettings _settings;
    private readonly ILogger<LiqPayService> _logger;

    public LiqPayService(
        HttpClient httpClient,
        IOptions<LiqPaySettings> options,
        ILogger<LiqPayService> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
    }

    public decimal GetPrice(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.Basic => _settings.BasicPriceUah,
        SubscriptionPlan.Pro => _settings.ProPriceUah,

        _ => throw new ArgumentException(
            "Free plan does not require payment.")
    };

    public LiqPayCheckoutResponse CreateCheckout(
        string orderId,
        SubscriptionPlan plan,
        decimal amount)
    {
        EnsureCheckoutConfigured();

        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException(
                "OrderId is required.",
                nameof(orderId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Amount must be greater than zero.");
        }

        var payload = new Dictionary<string, object?>
        {
            ["version"] = 7,
            ["public_key"] = _settings.PublicKey,
            ["action"] = "pay",
            ["amount"] = amount,
            ["currency"] = "UAH",
            ["description"] =
                $"Slotik {plan} subscription for 30 days",
            ["order_id"] = orderId,
            ["language"] = "uk",
            ["server_url"] = _settings.ServerUrl,
            ["result_url"] = _settings.ResultUrl
        };

        var json = JsonSerializer.Serialize(payload);

        var data = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(json));

        var signature = CreateSignature(data);

        return new LiqPayCheckoutResponse
        {
            CheckoutUrl = CheckoutUrl,
            Data = data,
            Signature = signature,
            OrderId = orderId
        };
    }

    public async Task<LiqPayRefundResponse> RefundAsync(
        string orderId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        EnsureKeysConfigured();

        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException(
                "OrderId is required.",
                nameof(orderId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Refund amount must be greater than zero.");
        }

        var payload = new Dictionary<string, object?>
        {
            ["version"] = 7,
            ["public_key"] = _settings.PublicKey,
            ["action"] = "refund",
            ["amount"] = amount,
            ["order_id"] = orderId
        };

        var responseBody = await SendApiRequestAsync(
            payload,
            "refund",
            orderId,
            cancellationToken);

        var result =
            DeserializeResponse<LiqPayRefundResponse>(
                responseBody,
                "refund",
                orderId);

        if (string.IsNullOrWhiteSpace(result.Result))
        {
            throw new InvalidOperationException(
                "LiqPay returned an incomplete refund response.");
        }

        _logger.LogInformation(
            "LiqPay refund response for order {OrderId}: " +
            "Result={Result}, Status={Status}, ErrorCode={ErrorCode}.",
            orderId,
            result.Result,
            result.Status,
            result.ErrorCode);

        return result;
    }

    public async Task<LiqPayPaymentStatusResponse>
        GetPaymentStatusAsync(
            string orderId,
            CancellationToken cancellationToken = default)
    {
        EnsureKeysConfigured();

        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException(
                "OrderId is required.",
                nameof(orderId));
        }

        var payload = new Dictionary<string, object?>
        {
            ["version"] = 7,
            ["public_key"] = _settings.PublicKey,
            ["action"] = "status",
            ["order_id"] = orderId
        };

        var responseBody = await SendApiRequestAsync(
            payload,
            "status",
            orderId,
            cancellationToken);

        var result =
            DeserializeResponse<LiqPayPaymentStatusResponse>(
                responseBody,
                "status",
                orderId);

        if (string.IsNullOrWhiteSpace(result.Result) ||
            string.IsNullOrWhiteSpace(result.Status))
        {
            throw new InvalidOperationException(
                "LiqPay returned an incomplete payment status response.");
        }

        _logger.LogInformation(
            "LiqPay payment status for order {OrderId}: " +
            "Result={Result}, Status={Status}, ErrorCode={ErrorCode}.",
            orderId,
            result.Result,
            result.Status,
            result.ErrorCode);

        return result;
    }

    public bool IsValidSignature(
        string data,
        string signature)
    {
        if (string.IsNullOrWhiteSpace(data) ||
            string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var expected = CreateSignature(data);

        try
        {
            var expectedBytes =
                Convert.FromBase64String(expected);

            var actualBytes =
                Convert.FromBase64String(signature);

            return System.Security.Cryptography
                .CryptographicOperations
                .FixedTimeEquals(
                    expectedBytes,
                    actualBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public LiqPayCallbackDto DecodeCallback(
        string data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            throw new InvalidOperationException(
                "LiqPay callback data is empty.");
        }

        var json = Encoding.UTF8.GetString(
            Convert.FromBase64String(data));

        return JsonSerializer.Deserialize<LiqPayCallbackDto>(
                   json,
                   JsonOptions)
               ?? throw new InvalidOperationException(
                   "Invalid LiqPay callback payload.");
    }

    private async Task<string> SendApiRequestAsync(
        Dictionary<string, object?> payload,
        string operation,
        string orderId,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);

        var data = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(json));

        var signature = CreateSignature(data);

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["data"] = data,
                ["signature"] = signature
            });

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.PostAsync(
                ApiRequestPath,
                content,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "LiqPay {Operation} request timed out " +
                "for order {OrderId}.",
                operation,
                orderId);

            throw new HttpRequestException(
                $"LiqPay {operation} request timed out.",
                null,
                HttpStatusCode.GatewayTimeout);
        }

        using (response)
        {
            var responseBody =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "LiqPay {Operation} HTTP failure " +
                    "for order {OrderId}. StatusCode={StatusCode}.",
                    operation,
                    orderId,
                    (int)response.StatusCode);

                throw new HttpRequestException(
                    $"LiqPay returned HTTP " +
                    $"{(int)response.StatusCode}.",
                    null,
                    response.StatusCode);
            }

            return responseBody;
        }
    }

    private T DeserializeResponse<T>(
        string responseBody,
        string operation,
        string orderId)
        where T : class
    {
        try
        {
            var result = JsonSerializer.Deserialize<T>(
                responseBody,
                JsonOptions);

            if (result == null)
            {
                throw new InvalidOperationException(
                    $"LiqPay returned an empty " +
                    $"{operation} response.");
            }

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "LiqPay returned invalid JSON for " +
                "{Operation} order {OrderId}.",
                operation,
                orderId);

            throw new InvalidOperationException(
                $"LiqPay returned an invalid " +
                $"{operation} response.",
                ex);
        }
    }

    private string CreateSignature(
        string data)
    {
        EnsureKeysConfigured();

        var raw = Encoding.UTF8.GetBytes(
            _settings.PrivateKey +
            data +
            _settings.PrivateKey);

        var digest = new Sha3Digest(256);

        digest.BlockUpdate(
            raw,
            0,
            raw.Length);

        var hash =
            new byte[digest.GetDigestSize()];

        digest.DoFinal(
            hash,
            0);

        return Convert.ToBase64String(hash);
    }

    private void EnsureKeysConfigured()
    {
        if (string.IsNullOrWhiteSpace(
                _settings.PublicKey))
        {
            throw new InvalidOperationException(
                "LiqPay PublicKey is not configured.");
        }

        if (string.IsNullOrWhiteSpace(
                _settings.PrivateKey))
        {
            throw new InvalidOperationException(
                "LiqPay PrivateKey is not configured.");
        }
    }

    private void EnsureCheckoutConfigured()
    {
        EnsureKeysConfigured();

        if (!Uri.TryCreate(
                _settings.ServerUrl,
                UriKind.Absolute,
                out var serverUri) ||
            serverUri.Scheme !=
            Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "LiqPay ServerUrl must be a valid HTTPS URL.");
        }

        if (!Uri.TryCreate(
                _settings.ResultUrl,
                UriKind.Absolute,
                out _))
        {
            throw new InvalidOperationException(
                "LiqPay ResultUrl is invalid.");
        }
    }
}