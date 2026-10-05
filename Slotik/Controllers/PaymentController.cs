using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;
using Slotik.Models.Enums;
using Slotik.Services;

namespace Slotik.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly LiqPayService _liqPay;
    private readonly LiqPaySettings _settings;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        AppDbContext context,
        LiqPayService liqPay,
        IOptions<LiqPaySettings> settings,
        IWebHostEnvironment environment,
        ILogger<PaymentController> logger)
    {
        _context = context;
        _liqPay = liqPay;
        _settings = settings.Value;
        _environment = environment;
        _logger = logger;
    }

    // ================================
    // CHECKOUT
    // ================================

    [HttpPost("checkout")]
    [Authorize(Roles = "Master")]
    public async Task<ActionResult<LiqPayCheckoutResponse>> CreateCheckout(
     [FromBody] CreatePaymentDto dto,
     CancellationToken cancellationToken)
    {
        if (dto.Plan is not (
            SubscriptionPlan.Basic or
            SubscriptionPlan.Pro))
        {
            return BadRequest(
                "Only Basic and Pro plans can be purchased.");
        }

        var email =
            User.FindFirstValue(
                JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized();

        var user = await _context.Users
            .Include(u => u.Master)
            .FirstOrDefaultAsync(
                u => u.Email == email,
                cancellationToken);

        if (user?.Master == null)
        {
            return BadRequest(
                "Master profile not found.");
        }

        decimal amount;

        try
        {
            amount = _liqPay.GetPrice(dto.Plan);
        }
        catch (ArgumentException)
        {
            return BadRequest(
                "Invalid subscription plan.");
        }

        if (amount <= 0)
        {
            return BadRequest(
                "Tariff price is not configured.");
        }

        var orderId =
            $"slotik-sub-{Guid.NewGuid():N}";

        await using var transaction =
            await _context.Database
                .BeginTransactionAsync(
                    cancellationToken);

        try
        {
            var subscription =
                new Subscription
                {
                    MasterId = user.Master.Id,
                    Plan = dto.Plan,
                    Status = SubscriptionStatus.Pending,
                    IsTrial = false,
                    ExpiresAt = DateTimeOffset.UtcNow
                };

            var payment =
                new Payment
                {
                    Subscription = subscription,
                    OrderId = orderId,
                    Amount = amount,
                    Currency = "UAH",
                    Status = PaymentStatus.Pending,
                    CreatedAt = DateTimeOffset.UtcNow
                };

            _context.Subscriptions.Add(subscription);
            _context.Payments.Add(payment);

            // Після SaveChanges payment.Id вже буде створений
            await _context.SaveChangesAsync(
                cancellationToken);

            var resultUrl =
                $"{_settings.ResultUrl.TrimEnd('/')}/payment/result?paymentId={payment.Id}";

            LiqPayCheckoutResponse checkout;

            try
            {
                checkout =
                    _liqPay.CreateCheckout(
                        orderId,
                        dto.Plan,
                        amount,
                        resultUrl);
            }
            catch (InvalidOperationException ex)
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                _logger.LogError(
                    ex,
                    "Unable to create LiqPay checkout " +
                    "for master {MasterId}.",
                    user.Master.Id);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "LiqPay is not configured correctly.");
            }

            await transaction.CommitAsync(
                cancellationToken);

            _logger.LogInformation(
                "Created LiqPay checkout {OrderId} " +
                "for master {MasterId}, plan {Plan}, " +
                "amount {Amount} {Currency}.",
                orderId,
                user.Master.Id,
                dto.Plan,
                amount,
                payment.Currency);

            return Ok(new
            {
                paymentId = payment.Id,
                checkout
            });
        }
        catch
        {
            await transaction.RollbackAsync(
                cancellationToken);

            throw;
        }
    }

    // ================================
    // CALLBACK
    // ================================

    [HttpPost("liqpay/callback")]
    [AllowAnonymous]
    [Consumes(
        "application/x-www-form-urlencoded")]
    public async Task<IActionResult>
        LiqPayCallback(
            [FromForm] string data,
            [FromForm] string signature,
            CancellationToken cancellationToken)
    {
        if (!_liqPay.IsValidSignature(
                data,
                signature))
        {
            _logger.LogWarning(
                "Rejected LiqPay callback " +
                "with invalid signature.");

            return Unauthorized(
                "Invalid LiqPay signature.");
        }

        LiqPayCallbackDto callback;

        try
        {
            callback =
                _liqPay.DecodeCallback(data);
        }
        catch (Exception ex)
            when (
                ex is FormatException
                or System.Text.Json.JsonException
                or InvalidOperationException)
        {
            _logger.LogWarning(
                ex,
                "Rejected invalid LiqPay callback payload.");

            return BadRequest(
                "Invalid LiqPay callback data.");
        }

        if (!string.Equals(
                callback.PublicKey,
                _settings.PublicKey,
                StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Rejected LiqPay callback " +
                "for a different merchant. " +
                "OrderId={OrderId}.",
                callback.OrderId);

            return Unauthorized(
                "Wrong LiqPay merchant.");
        }

        var callbackStatus =
            NormalizeStatus(
                callback.Status);

        if (callbackStatus == "sandbox" &&
            !_environment.IsDevelopment() &&
            !_environment.IsStaging())
        {
            _logger.LogWarning(
                "Rejected sandbox callback " +
                "outside Development/Staging. " +
                "OrderId={OrderId}.",
                callback.OrderId);

            return BadRequest(
                "Sandbox payment is not allowed " +
                "in production.");
        }

        await using var transaction =
            await _context.Database
                .BeginTransactionAsync(
                    cancellationToken);

        var payment =
            await _context.Payments
                .Include(
                    p => p.Subscription)
                .FirstOrDefaultAsync(
                    p =>
                        p.OrderId ==
                        callback.OrderId,
                    cancellationToken);

        if (payment == null)
        {
            await transaction.RollbackAsync(
                cancellationToken);

            _logger.LogWarning(
                "LiqPay callback references " +
                "unknown OrderId={OrderId}.",
                callback.OrderId);

            return NotFound(
                "Payment not found.");
        }

        await _context.Database
            .ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({payment.Subscription.MasterId})",
                cancellationToken);

        await _context.Entry(payment)
            .ReloadAsync(
                cancellationToken);

        await _context
            .Entry(payment.Subscription)
            .ReloadAsync(
                cancellationToken);

        if (callback.Amount !=
                payment.Amount ||
            !string.Equals(
                callback.Currency,
                payment.Currency,
                StringComparison.OrdinalIgnoreCase))
        {
            await transaction.RollbackAsync(
                cancellationToken);

            _logger.LogWarning(
                "LiqPay amount/currency mismatch " +
                "for OrderId={OrderId}. " +
                "Expected={ExpectedAmount} " +
                "{ExpectedCurrency}, " +
                "Received={ReceivedAmount} " +
                "{ReceivedCurrency}.",
                payment.OrderId,
                payment.Amount,
                payment.Currency,
                callback.Amount,
                callback.Currency);

            return BadRequest(
                "Payment amount or currency mismatch.");
        }

        var previousProviderStatus =
            NormalizeStatus(
                payment.ProviderStatus);

        var providerPaymentId =
            callback.PaymentId?.ToString();

        // Reversed is terminal and may
        // override success/failure.
        if (callbackStatus == "reversed")
        {
            ApplyReversedState(
                payment,
                providerPaymentId);

            await _context.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            _logger.LogInformation(
                "Payment {PaymentId} / {OrderId} " +
                "was reversed by LiqPay.",
                payment.Id,
                payment.OrderId);

            return Ok();
        }

        // A stale success callback must never
        // resurrect a refunded payment.
        if (previousProviderStatus ==
            "reversed")
        {
            await transaction.CommitAsync(
                cancellationToken);

            _logger.LogWarning(
                "Ignored LiqPay status " +
                "{CallbackStatus} for already " +
                "reversed payment {PaymentId} / " +
                "{OrderId}.",
                callbackStatus,
                payment.Id,
                payment.OrderId);

            return Ok();
        }

        var isSuccessful =
            callbackStatus is
                "success" or
                "sandbox";

        if (isSuccessful)
        {
            // Duplicate successful callback.
            if (payment.Status ==
                PaymentStatus.Success)
            {
                payment.ProviderPaymentId =
                    providerPaymentId ??
                    payment.ProviderPaymentId;

                payment.ProviderStatus =
                    callbackStatus;

                await _context.SaveChangesAsync(
                    cancellationToken);

                await transaction.CommitAsync(
                    cancellationToken);

                return Ok();
            }

            // failure/error are treated as
            // terminal. Only reversed can
            // override a final state.
            if (payment.Status ==
                PaymentStatus.Failed)
            {
                await transaction.CommitAsync(
                    cancellationToken);

                _logger.LogWarning(
                    "Ignored late successful " +
                    "callback for failed payment " +
                    "{PaymentId} / {OrderId}.",
                    payment.Id,
                    payment.OrderId);

                return Ok();
            }

            payment.ProviderPaymentId =
                providerPaymentId ??
                payment.ProviderPaymentId;

            payment.ProviderStatus =
                callbackStatus;

            payment.Status =
                PaymentStatus.Success;

            payment.PaidAt =
                DateTimeOffset.UtcNow;

            var now =
                DateTimeOffset.UtcNow;

            var subscription =
                payment.Subscription;

            // ========================================
            // ACTIVE PRO TRIAL
            // ========================================

            var activeTrial =
                await _context.Subscriptions
                    .Where(s =>
                        s.MasterId ==
                            subscription.MasterId &&

                        s.Id !=
                            subscription.Id &&

                        s.Status ==
                            SubscriptionStatus.Active &&

                        s.IsTrial &&

                        s.Plan ==
                            SubscriptionPlan.Pro &&

                        s.ExpiresAt > now)
                    .OrderByDescending(
                        s => s.ExpiresAt)
                    .FirstOrDefaultAsync(
                        cancellationToken);

            // ========================================
            // ACTIVE PAID SUBSCRIPTION
            // ========================================

            var activePaidSubscription =
                await _context.Subscriptions
                    .Where(s =>
                        s.MasterId ==
                            subscription.MasterId &&

                        s.Id !=
                            subscription.Id &&

                        s.Status ==
                            SubscriptionStatus.Active &&

                        !s.IsTrial &&

                        s.Plan !=
                            SubscriptionPlan.Free &&

                        s.ExpiresAt > now)
                    .OrderByDescending(
                        s => s.ExpiresAt)
                    .FirstOrDefaultAsync(
                        cancellationToken);

            subscription.IsTrial = false;

            // ========================================
            // TRIAL PRO -> PAID PRO
            // ========================================

            if (subscription.Plan ==
                    SubscriptionPlan.Pro &&
                activeTrial != null)
            {
                var baseExpiresAt =
                    activeTrial.ExpiresAt;

                if (activePaidSubscription != null &&
                    activePaidSubscription.Plan ==
                        SubscriptionPlan.Pro &&
                    activePaidSubscription.ExpiresAt >
                        baseExpiresAt)
                {
                    baseExpiresAt =
                        activePaidSubscription.ExpiresAt;
                }

                subscription.ExpiresAt =
                    baseExpiresAt.AddDays(30);

                activeTrial.Status =
                    SubscriptionStatus.Cancelled;

                // If there was an active Basic or previous Pro,
                // the new paid Pro becomes the active subscription.
                if (activePaidSubscription != null)
                {
                    activePaidSubscription.Status =
                        SubscriptionStatus.Cancelled;
                }
            }

            // ========================================
            // SAME PLAN RENEWAL
            // ========================================

            // Basic -> Basic
            // or Pro -> Pro.
            else if (
                activePaidSubscription != null &&
                activePaidSubscription.Plan ==
                    subscription.Plan)
            {
                subscription.ExpiresAt =
                    activePaidSubscription
                        .ExpiresAt
                        .AddDays(30);

                activePaidSubscription.Status =
                    SubscriptionStatus.Cancelled;
            }

            // ========================================
            // NEW PLAN / PLAN SWITCH
            // ========================================

            // 
            // Free -> Basic
            // Free -> Pro
            // Basic -> Pro
            // Pro -> Basic
            else
            {
                if (activePaidSubscription != null)
                {
                    activePaidSubscription.Status =
                        SubscriptionStatus.Cancelled;
                }

                subscription.ExpiresAt =
                    now.AddDays(30);
            }

            subscription.Status =
                SubscriptionStatus.Active;

            await _context.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            _logger.LogInformation(
                "Payment {PaymentId} / {OrderId} " +
                "succeeded. Subscription " +
                "{SubscriptionId} active until " +
                "{ExpiresAt}.",
                payment.Id,
                payment.OrderId,
                subscription.Id,
                subscription.ExpiresAt);

            return Ok();
        }

        var isFailed =
            callbackStatus is
                "failure" or
                "error";

        if (isFailed)
        {
            // Do not let a stale failure
            // destroy an already successful
            // purchase.
            if (payment.Status ==
                PaymentStatus.Success)
            {
                await transaction.CommitAsync(
                    cancellationToken);

                _logger.LogWarning(
                    "Ignored late LiqPay failure " +
                    "{CallbackStatus} for successful " +
                    "payment {PaymentId} / {OrderId}.",
                    callbackStatus,
                    payment.Id,
                    payment.OrderId);

                return Ok();
            }

            payment.ProviderPaymentId =
                providerPaymentId ??
                payment.ProviderPaymentId;

            payment.ProviderStatus =
                callbackStatus;

            payment.Status =
                PaymentStatus.Failed;

            payment.Subscription.Status =
                SubscriptionStatus.Cancelled;

            await _context.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            _logger.LogInformation(
                "Payment {PaymentId} / {OrderId} " +
                "failed with LiqPay status " +
                "{CallbackStatus}.",
                payment.Id,
                payment.OrderId,
                callbackStatus);

            return Ok();
        }

        // Intermediate provider states should
        // only be stored while our payment
        // itself is Pending.
        if (payment.Status ==
                PaymentStatus.Pending &&
            !IsTerminalProviderStatus(
                previousProviderStatus))
        {
            payment.ProviderPaymentId =
                providerPaymentId ??
                payment.ProviderPaymentId;

            payment.ProviderStatus =
                callbackStatus;

            await _context.SaveChangesAsync(
                cancellationToken);
        }
        else
        {
            _logger.LogInformation(
                "Ignored intermediate LiqPay status " +
                "{CallbackStatus} for terminal " +
                "payment {PaymentId} / {OrderId}.",
                callbackStatus,
                payment.Id,
                payment.OrderId);
        }

        await transaction.CommitAsync(
            cancellationToken);

        return Ok();
    }

    // ================================
    // DEV / STAGING REFUND
    // ================================

    [HttpPost(
        "dev/refund/{paymentId:int}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> DevRefund(
        int paymentId,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment() &&
            !_environment.IsStaging())
        {
            return NotFound();
        }

        var payment =
            await _context.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.Id == paymentId,
                    cancellationToken);

        if (payment == null)
        {
            return NotFound(
                "Payment not found.");
        }

        if (NormalizeStatus(
                payment.ProviderStatus) ==
            "reversed")
        {
            return Conflict(new
            {
                error =
                    "already_reversed",

                message =
                    "This payment has already " +
                    "been reversed."
            });
        }

        if (payment.Status !=
            PaymentStatus.Success)
        {
            return Conflict(new
            {
                error =
                    "payment_not_successful",

                message =
                    "Only successful payments " +
                    "can be refunded."
            });
        }

        if (string.IsNullOrWhiteSpace(
                payment.OrderId))
        {
            return BadRequest(new
            {
                error =
                    "missing_order_id",

                message =
                    "Payment has no " +
                    "LiqPay OrderId."
            });
        }

        LiqPayRefundResponse refundResponse;

        try
        {
            refundResponse =
                await _liqPay.RefundAsync(
                    payment.OrderId,
                    payment.Amount,
                    cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "LiqPay transport error while " +
                "refunding payment {PaymentId} / " +
                "{OrderId}.",
                payment.Id,
                payment.OrderId);

            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    error =
                        "liqpay_transport_error",

                    message =
                        "Could not complete the " +
                        "request to LiqPay."
                });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(
                ex,
                "Invalid LiqPay refund response " +
                "for payment {PaymentId} / " +
                "{OrderId}.",
                payment.Id,
                payment.OrderId);

            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    error =
                        "liqpay_invalid_response",

                    message =
                        "LiqPay returned an " +
                        "invalid response."
                });
        }

        if (!refundResponse.IsSuccess)
        {
            _logger.LogWarning(
                "LiqPay rejected refund for " +
                "payment {PaymentId} / {OrderId}. " +
                "ErrorCode={ErrorCode}.",
                payment.Id,
                payment.OrderId,
                refundResponse.ErrorCode);

            /*
             * Important case:
             *
             * Our previous test already
             * produced a successful reversed
             * response but callback never
             * arrived.
             *
             * A second refund produced:
             * amount_limit.
             *
             * Instead of assuming failure,
             * ask LiqPay for the real current
             * status of the payment.
             */
            if (string.Equals(
                    refundResponse.ErrorCode,
                    "amount_limit",
                    StringComparison.OrdinalIgnoreCase))
            {
                LiqPayPaymentStatusResponse?
                    providerStatus = null;

                try
                {
                    providerStatus =
                        await _liqPay
                            .GetPaymentStatusAsync(
                                payment.OrderId,
                                cancellationToken);
                }
                catch (Exception ex)
                    when (
                        ex is HttpRequestException
                        or InvalidOperationException)
                {
                    _logger.LogWarning(
                        ex,
                        "Could not reconcile " +
                        "amount_limit for payment " +
                        "{PaymentId} / {OrderId}.",
                        payment.Id,
                        payment.OrderId);
                }

                if (providerStatus is
                    {
                        IsSuccess: true,
                        IsReversed: true
                    } &&
                    IsMatchingPaymentStatus(
                        providerStatus,
                        payment))
                {
                    var reconciled =
                        await ApplyReversedLocallyAsync(
                            payment.Id,
                            providerStatus
                                .PaymentId?
                                .ToString(),
                            cancellationToken);

                    if (reconciled == null)
                    {
                        return NotFound(
                            "Payment not found " +
                            "during reconciliation.");
                    }

                    return Ok(new
                    {
                        refundAccepted = true,
                        appliedLocally = true,
                        reconciled = true,

                        paymentId =
                            reconciled.Id,

                        reconciled.OrderId,
                        reconciled.Amount,

                        refundResponse,

                        currentProviderStatus =
                            providerStatus
                    });
                }

                return Conflict(new
                {
                    refundAccepted = false,

                    paymentId =
                        payment.Id,

                    payment.OrderId,
                    payment.Amount,

                    refundResponse,

                    currentProviderStatus =
                        providerStatus
                });
            }

            // LiqPay answered normally,
            // but rejected the business
            // operation itself.
            return StatusCode(
                StatusCodes
                    .Status422UnprocessableEntity,
                new
                {
                    refundAccepted = false,

                    paymentId =
                        payment.Id,

                    payment.OrderId,
                    payment.Amount,

                    provider =
                        refundResponse
                });
        }

        /*
         * LiqPay itself returned:
         *
         * result = ok
         * status = reversed
         *
         * That's already a final provider
         * state, so update our DB immediately.
         * We do not depend entirely on a
         * possibly delayed/missing callback.
         *
         * If callback arrives later,
         * handling remains idempotent.
         */
        if (refundResponse.IsReversed)
        {
            var reversed =
                await ApplyReversedLocallyAsync(
                    payment.Id,
                    refundResponse
                        .PaymentId?
                        .ToString(),
                    cancellationToken);

            if (reversed == null)
            {
                return NotFound(
                    "Payment not found " +
                    "after refund request.");
            }

            return Ok(new
            {
                refundAccepted = true,
                appliedLocally = true,
                reconciled = false,

                paymentId =
                    reversed.Id,

                reversed.OrderId,
                reversed.Amount,

                provider =
                    refundResponse
            });
        }

        // LiqPay accepted the request,
        // but did not return a final reversed
        // state yet.
        return Accepted(new
        {
            refundAccepted = true,
            appliedLocally = false,
            reconciled = false,

            paymentId =
                payment.Id,

            payment.OrderId,
            payment.Amount,

            provider =
                refundResponse
        });
    }

    // ================================
    // GET PAYMENT
    // ================================

    [HttpGet("{id:int}")]
    [Authorize(
        Roles = "Master,Superadmin")]
    public async Task<IActionResult> GetPayment(
        int id,
        CancellationToken cancellationToken)
    {
        var payment =
            await _context.Payments
                .AsNoTracking()
                .Include(
                    p => p.Subscription)
                    .ThenInclude(
                        s => s.Master)
                        .ThenInclude(
                            m => m.User)
                .FirstOrDefaultAsync(
                    p => p.Id == id,
                    cancellationToken);

        if (payment == null)
            return NotFound();

        if (User.IsInRole("Superadmin"))
            return Ok(payment);

        var email =
            User.FindFirstValue(
                JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized();

        if (!string.Equals(
                payment.Subscription
                    .Master
                    .User
                    .Email,
                email,
                StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        return Ok(payment);
    }

    // ================================
    // HELPERS
    // ================================

    private async Task<Payment?>
        ApplyReversedLocallyAsync(
            int paymentId,
            string? providerPaymentId,
            CancellationToken cancellationToken)
    {
        await using var transaction =
            await _context.Database
                .BeginTransactionAsync(
                    cancellationToken);

        var payment =
            await _context.Payments
                .Include(
                    p => p.Subscription)
                .FirstOrDefaultAsync(
                    p => p.Id == paymentId,
                    cancellationToken);

        if (payment == null)
        {
            await transaction.RollbackAsync(
                cancellationToken);

            return null;
        }

        await _context.Database
            .ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({payment.Subscription.MasterId})",
                cancellationToken);

        await _context.Entry(payment)
            .ReloadAsync(
                cancellationToken);

        await _context
            .Entry(payment.Subscription)
            .ReloadAsync(
                cancellationToken);

        ApplyReversedState(
            payment,
            providerPaymentId);

        await _context.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return payment;
    }

    private static bool
        IsMatchingPaymentStatus(
            LiqPayPaymentStatusResponse
                providerStatus,
            Payment payment)
    {
        var orderMatches =
            string.IsNullOrWhiteSpace(
                providerStatus.OrderId)
            ||
            string.Equals(
                providerStatus.OrderId,
                payment.OrderId,
                StringComparison.Ordinal);

        var amountMatches =
            !providerStatus.Amount.HasValue
            ||
            providerStatus.Amount.Value ==
            payment.Amount;

        var currencyMatches =
            string.IsNullOrWhiteSpace(
                providerStatus.Currency)
            ||
            string.Equals(
                providerStatus.Currency,
                payment.Currency,
                StringComparison
                    .OrdinalIgnoreCase);

        return
            orderMatches &&
            amountMatches &&
            currencyMatches;
    }

    private static void ApplyReversedState(
        Payment payment,
        string? providerPaymentId)
    {
        payment.ProviderPaymentId =
            !string.IsNullOrWhiteSpace(
                providerPaymentId)
                ? providerPaymentId
                : payment.ProviderPaymentId;

        payment.ProviderStatus =
            "reversed";

        payment.Status =
            PaymentStatus.Failed;

        payment.Subscription.Status =
            SubscriptionStatus.Cancelled;
    }

    private static string NormalizeStatus(
        string? status)
    {
        return status?
                   .Trim()
                   .ToLowerInvariant()
               ?? string.Empty;
    }

    private static bool
        IsTerminalProviderStatus(
            string status)
    {
        return status is
            "success"
            or "sandbox"
            or "failure"
            or "error"
            or "reversed";
    }
}