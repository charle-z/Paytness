using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Payments;
using Nop.Services.Orders;
using Nop.Web.Framework.Controllers;

namespace Paytness.Reference;

public sealed class PaytnessReferenceWebhookController(
    ReferenceState referenceState,
    IOrderService orderService,
    IOrderProcessingService orderProcessingService) : BasePluginController
{
    private static readonly JsonSerializerOptions WebhookJson = new(JsonSerializerDefaults.Web);
    private const int MaximumBodyBytes = 256 * 1024;

    [HttpPost]
    public async Task<IActionResult> Webhook()
    {
        if (!ReferenceMode.Enabled) return NotFound();
        if (Request.ContentLength is > MaximumBodyBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge);

        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, HttpContext.RequestAborted);
        if (buffer.Length > MaximumBodyBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        byte[] body = buffer.ToArray();

        ReferenceWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ReferenceWebhookPayload>(body, WebhookJson);
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "invalid_webhook_json" });
        }

        if (payload is null
            || string.IsNullOrWhiteSpace(payload.EventId)
            || string.IsNullOrWhiteSpace(payload.EventType)
            || !Guid.TryParse(payload.LogicalPayment, out Guid logicalPayment))
            return BadRequest(new { error = "invalid_webhook_payload" });

        referenceState.SetCurrent(logicalPayment);
        ReferenceState.PaymentState paymentState = referenceState.Get(logicalPayment);
        paymentState.RecordWebhookReceived(payload.EventType);

        string? webhookSecret = Environment.GetEnvironmentVariable("PAYTNESS_WEBHOOK_SECRET");
        string signature = Request.Headers["X-Paytness-Signature"].ToString();
        if (string.IsNullOrEmpty(webhookSecret) || !ValidateSignature(body, signature, webhookSecret))
        {
            paymentState.RecordSignatureFailure();
            return Unauthorized();
        }

        if (!paymentState.TryRecordWebhookApplied(payload.EventId)) return Ok();

        if (string.Equals(payload.EventType, "payment.succeeded", StringComparison.Ordinal))
            await ApplySucceededAsync(logicalPayment, paymentState, HttpContext.RequestAborted);
        else if (string.Equals(payload.EventType, "payment.processing", StringComparison.Ordinal) && ReferenceMode.AcceptStaleState)
            await ApplyStaleProcessingAsync(logicalPayment, paymentState, HttpContext.RequestAborted);

        return Ok();
    }

    private async Task ApplySucceededAsync(Guid logicalPayment, ReferenceState.PaymentState paymentState, CancellationToken cancellationToken)
    {
        await paymentState.PaymentApplicationGate.WaitAsync(cancellationToken);
        try
        {
            var order = await orderService.GetOrderByGuidAsync(logicalPayment);
            if (order is not null && order.PaymentStatus != PaymentStatus.Paid)
                await orderProcessingService.MarkOrderAsPaidAsync(order);
        }
        finally
        {
            paymentState.PaymentApplicationGate.Release();
        }
    }

    private async Task ApplyStaleProcessingAsync(Guid logicalPayment, ReferenceState.PaymentState paymentState, CancellationToken cancellationToken)
    {
        await paymentState.PaymentApplicationGate.WaitAsync(cancellationToken);
        try
        {
            var order = await orderService.GetOrderByGuidAsync(logicalPayment);
            if (order is not null && order.PaymentStatus == PaymentStatus.Paid)
            {
                order.PaymentStatus = PaymentStatus.Pending;
                await orderService.UpdateOrderAsync(order);
            }
        }
        finally
        {
            paymentState.PaymentApplicationGate.Release();
        }
    }

    private static bool ValidateSignature(byte[] body, string signature, string secret)
    {
        const string prefix = "sha256=";
        if (!signature.StartsWith(prefix, StringComparison.Ordinal) || signature.Length <= prefix.Length) return false;

        byte[] actual;
        try
        {
            actual = Convert.FromHexString(signature[prefix.Length..]);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] key = Encoding.UTF8.GetBytes(secret);
        try
        {
            byte[] expected = HMACSHA256.HashData(key, body);
            return actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(actual);
        }
    }
}
