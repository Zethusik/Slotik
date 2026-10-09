using Microsoft.AspNetCore.RateLimiting;
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
    private readonly IMasterSubscriptionLock _masterLock;

    public PaymentController(
        AppDbContext context,
        LiqPayService liqPay,
        IOptions<LiqPaySettings> settings,
        IWebHostEnvironment environment,
        ILogger<PaymentController> logger, IMasterSubscriptionLock masterLock)
    {
        _context = context;
        _liqPay = liqPay;
        _settings = settings.Value;
        _environment = environment;
        _logger = logger;
        _masterLock = masterLock;
    }

    // ================================
    // CHECKOUT
    // ================================

    [HttpPost("checkout")]
    [EnableRateLimiting("checkout")]
    [Authorize(Roles = "Master")]
    public async Task<ActionResult<LiqPayCheckoutResponse>> CreateCheckout(
     [FromBody] CreatePaymentDto dto,
     CancellationToken cancellationToken)
    {
        if (!_settings.IsEnabled) return StatusCode(503, "Payments are not configured.");
        if (dto.Plan is not (
            SubscriptionPlan.Basic or
            SubscriptionPlan.Pro))
        {
            return BadRequest(
                "Only Basic and Pro plans can be purchased.");
        }

        var userId = User.UserId();

        if (!userId.HasValue)
            return Unauthorized();

        var user = await _context.Users
            .Include(u => u.Master)
            .FirstOrDefaultAsync(
                u => u.Id == userId.Value,
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

        await using var transaction = await _masterLock.AcquireAsync(user.Master.Id, cancellationToken);
        if (await _context.Subscriptions.AnyAsync(s => s.MasterId == user.Master.Id
            && s.Plan == dto.Plan && !s.IsTrial && s.Status == SubscriptionStatus.Active
            && s.ExpiresAt > DateTimeOffset.UtcNow, cancellationToken))
            return Conflict(new { error = "active_plan_already_exists",
                message = "Wait until your current subscription to this plan expires before buying it again." });

        var orderId = $"slotik-sub-{Guid.NewGuid():N}";
        var subscription = new Subscription { MasterId = user.Master.Id, Plan = dto.Plan,
            Status = SubscriptionStatus.Pending, IsTrial = false, ExpiresAt = DateTimeOffset.UtcNow };
        var payment = new Payment { Subscription = subscription, OrderId = orderId, Amount = amount,
            Currency = "UAH", Status = PaymentStatus.Pending, CreatedAt = DateTimeOffset.UtcNow };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);
        var resultUrl = $"{_settings.ResultUrl.TrimEnd('/')}/payment/result?paymentId={payment.Id}";
        LiqPayCheckoutResponse checkout;
        try { checkout = _liqPay.CreateCheckout(orderId, dto.Plan, amount, resultUrl); }
        catch (InvalidOperationException)
        {
            _logger.LogError("Unable to create LiqPay checkout for master {MasterId}.", user.Master.Id);
            // Disposing the uncommitted lease rolls back the pending rows.
            return StatusCode(500, "LiqPay is not configured correctly.");
        }
        await transaction.CommitAsync(cancellationToken);
        return Ok(new { paymentId = payment.Id, checkout });
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
        if (!_settings.IsEnabled) return StatusCode(503, "Payments are not configured.");
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

        return await ApplyProviderStatusAsync(callback, cancellationToken);
    }

    private async Task<IActionResult> ApplyProviderStatusAsync(LiqPayCallbackDto callback, CancellationToken cancellationToken)
    {
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

        var ownerId = await _context.Payments.AsNoTracking().Where(p => p.OrderId == callback.OrderId)
            .Select(p => (int?)p.Subscription.MasterId).SingleOrDefaultAsync(cancellationToken);
        if (ownerId == null) return NotFound("Payment not found.");
        await using var transaction = await _masterLock.AcquireAsync(ownerId.Value, cancellationToken);
        var payment = await _context.Payments.Include(p => p.Subscription)
            .SingleOrDefaultAsync(p => p.OrderId == callback.OrderId, cancellationToken);
        if (payment == null) return NotFound("Payment not found.");
        // Reconciliation may already track these entities. Refresh under the lock.
        await _context.Entry(payment).ReloadAsync(cancellationToken);
        await _context.Entry(payment.Subscription).ReloadAsync(cancellationToken);
        if (payment.Subscription.MasterId != ownerId.Value)
            return Conflict(new { error = "payment_owner_changed" });
        if (callback.Amount !=
                payment.Amount ||
            !string.Equals(
                callback.Currency,
                payment.Currency,
                StringComparison.OrdinalIgnoreCase))
        {
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
            await ApplyReversedStateAsync(payment, providerPaymentId, cancellationToken);

            if (payment.EntitlementReviewRequired)
                _logger.LogError("Refund entitlement requires historical allocation review. PaymentId={PaymentId}, OrderId={OrderId}.", payment.Id, payment.OrderId);

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

        var isSuccessful = LiqPayStatusPolicy.IsSuccessful(callbackStatus);

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

            var activated = await PaidPlanActivation.ApplyAsync(_context, payment, now, cancellationToken);
            if (!activated)
                _logger.LogWarning("Successful payment {PaymentId} requires entitlement review; no additional access was granted.", payment.Id);
            await OnboardingService.SaveAndCompleteAsync(_context, subscription.MasterId, cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            _logger.LogInformation(
                "Payment {PaymentId} / {OrderId} " +
                "succeeded. Subscription " +
                "{SubscriptionId}, activated={Activated}, expires " +
                "{ExpiresAt}.",
                payment.Id,
                payment.OrderId,
                subscription.Id,
                activated,
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

        if (!LiqPayStatusPolicy.IsIntermediate(callbackStatus))
            _logger.LogWarning("Unrecognized LiqPay state {Status} for order {OrderId}; retained without activation.", callbackStatus, payment.OrderId);
        else
            _logger.LogInformation("LiqPay intermediate state {Status} for order {OrderId}.", callbackStatus, payment.OrderId);
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

        if (!await _context.EntitlementGrants.AnyAsync(g => g.PaymentId == payment.Id, cancellationToken))
            return Conflict(new { error = "legacy_entitlement_review_required",
                message = "Historical payment allocation must be reviewed before requesting a refund." });
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

    [HttpPost("{id:int}/reconcile")]
    [Authorize(Roles = "Master,Superadmin")]
    [EnableRateLimiting("reconcile")]
    public async Task<IActionResult> Reconcile(int id, CancellationToken cancellationToken)
    {
        if (!_settings.IsEnabled) return StatusCode(503, "Payments are not configured.");
        if (User.UserId() is not int userId) return Unauthorized();
        var isAdmin = User.IsInRole("Superadmin");
        var payment = await _context.Payments.Include(p => p.Subscription).ThenInclude(s => s.Master)
            .SingleOrDefaultAsync(p => p.Id == id && (isAdmin || p.Subscription.Master.UserId == userId), cancellationToken);
        if (payment == null) return NotFound();
        // Claim a bounded attempt durably before network IO; callbacks are free to finish meanwhile.
        await using (var transaction = await _context.Database.BeginTransactionAsync(cancellationToken))
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({payment.Subscription.MasterId})", cancellationToken);
            await _context.Entry(payment).ReloadAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            if (payment.Status != PaymentStatus.Pending) return Ok(ApiResponses.Payment(payment));
            if (payment.CreatedAt < now.AddDays(-_settings.ReconciliationMaxAgeDays)
                || payment.ReconciliationAttempts >= _settings.MaxReconciliationAttempts)
                return Conflict(new { error = "reconciliation_limit", message = "Operator investigation required." });
            if (payment.LastReconciledAt > now.AddSeconds(-_settings.ReconciliationIntervalSeconds))
                return StatusCode(429, new { error = "reconciliation_cooldown" });
            payment.LastReconciledAt = now; payment.ReconciliationAttempts++;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        LiqPayPaymentStatusResponse status;
        try { status = await _liqPay.GetPaymentStatusAsync(payment.OrderId, cancellationToken); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _logger.LogWarning("LiqPay reconciliation failed for payment {PaymentId}: {ErrorType}.", id, ex.GetType().Name);
            return StatusCode(502, new { error = "provider_unavailable" });
        }
        // Never accept a partial status response as proof of payment ownership or amount.
        if (!status.IsSuccess || status.PublicKey != _settings.PublicKey || status.OrderId != payment.OrderId
            || status.Amount != payment.Amount || !string.Equals(status.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase)
            || status.PaymentId is not > 0
            || (payment.ProviderPaymentId != null && payment.ProviderPaymentId != status.PaymentId.ToString()))
            return StatusCode(502, new { error = "provider_status_mismatch" });
        var applied = await ApplyProviderStatusAsync(new LiqPayCallbackDto
        {
            PublicKey = status.PublicKey, OrderId = status.OrderId!, Amount = status.Amount!.Value,
            Currency = status.Currency!, PaymentId = status.PaymentId, Status = status.Status!
        }, cancellationToken);
        if (applied is not OkResult) return applied;
        await _context.Entry(payment).ReloadAsync(cancellationToken);
        return Ok(ApiResponses.Payment(payment));
    }

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
            return Ok(ApiResponses.Payment(payment));

        var userId = User.UserId();

        if (!userId.HasValue)
            return Unauthorized();

        if (payment.Subscription.Master.UserId != userId.Value)
        {
            return Forbid();
        }

        return Ok(ApiResponses.Payment(payment));
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

        await ApplyReversedStateAsync(payment, providerPaymentId, cancellationToken);

        await _context.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return payment;
    }

    private bool IsMatchingPaymentStatus(LiqPayPaymentStatusResponse status, Payment payment) =>
        status.IsSuccess && status.PublicKey == _settings.PublicKey
        && status.OrderId == payment.OrderId && status.Amount == payment.Amount
        && string.Equals(status.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase)
        && status.PaymentId is > 0
        && (payment.ProviderPaymentId == null || payment.ProviderPaymentId == status.PaymentId.ToString());
    private async Task ApplyReversedStateAsync(
        Payment payment,
        string? providerPaymentId, CancellationToken cancellationToken)
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

        await EntitlementService.RevokeAsync(_context, payment, DateTimeOffset.UtcNow, cancellationToken);
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
            or "sandbox" or "wait_compensation"
            or "failure"
            or "error"
            or "reversed";
    }
}
