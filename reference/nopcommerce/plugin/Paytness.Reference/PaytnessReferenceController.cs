using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Tax;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework.Controllers;

namespace Paytness.Reference;

public sealed class PaytnessReferenceController(
    ReferenceState referenceState,
    IStoreContext storeContext,
    ICustomerService customerService,
    ILanguageService languageService,
    IAddressService addressService,
    IOrderService orderService,
    IPaymentService paymentService,
    IOrderProcessingService orderProcessingService) : BasePluginController
{
    private const string ReferenceAdminEmail = "paytness-reference@example.invalid";

    [HttpPost]
    public async Task<IActionResult> Pay([FromBody] ReferencePayRequest request)
    {
        if (!ReferenceMode.Enabled) return NotFound();
        if (!Guid.TryParse(request.LogicalPayment, out Guid logicalPayment))
            return BadRequest(new { error = "logical_payment_must_be_guid" });
        if (request.AmountMinor is < 1 or > 100_000_000)
            return BadRequest(new { error = "amount_minor_out_of_range" });

        referenceState.SetCurrent(logicalPayment);
        decimal orderTotal = request.AmountMinor / 100m;
        Order order = await EnsureOrderAsync(logicalPayment, orderTotal, HttpContext.RequestAborted);

        var paymentRequest = new ProcessPaymentRequest
        {
            StoreId = order.StoreId,
            CustomerId = order.CustomerId,
            OrderGuid = order.OrderGuid,
            OrderGuidGeneratedOnUtc = order.CreatedOnUtc,
            OrderTotal = order.OrderTotal,
            PaymentMethodSystemName = ReferencePaymentMethod.SystemName,
        };

        ProcessPaymentResult result = await paymentService.ProcessPaymentAsync(paymentRequest);
        if (!result.Success && request.RetryOnAmbiguity)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), HttpContext.RequestAborted);
            result = await paymentService.ProcessPaymentAsync(paymentRequest);
        }

        if (!result.Success && !request.RetryOnAmbiguity)
            return Accepted(new { logicalPayment = logicalPayment.ToString("N"), orderId = order.Id, paymentStatus = order.PaymentStatus.ToString(), confirmed = false });

        if (!result.Success || result.NewPaymentStatus != PaymentStatus.Paid)
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "payment_not_confirmed" });

        ReferenceState.PaymentState paymentState = referenceState.Get(logicalPayment);
        await paymentState.PaymentApplicationGate.WaitAsync(HttpContext.RequestAborted);
        try
        {
            order = await orderService.GetOrderByGuidAsync(logicalPayment)
                ?? throw new InvalidOperationException("Reference order disappeared.");
            if (order.PaymentStatus != PaymentStatus.Paid)
            {
                order.CaptureTransactionId = result.CaptureTransactionId;
                await orderProcessingService.MarkOrderAsPaidAsync(order);
            }
        }
        finally
        {
            paymentState.PaymentApplicationGate.Release();
        }

        return Ok(new
        {
            logicalPayment = logicalPayment.ToString("N"),
            orderId = order.Id,
            paymentStatus = PaymentStatus.Paid.ToString(),
        });
    }

    [HttpGet]
    public async Task<IActionResult> State()
    {
        if (!ReferenceMode.Enabled) return NotFound();
        if (!referenceState.TryGetCurrent(out Guid logicalPayment))
            return Ok(new { paymentStatus = "Missing", orderPaidEventCount = 0 });

        Order? order = await orderService.GetOrderByGuidAsync(logicalPayment);
        ReferenceSnapshot snapshot = referenceState.Get(logicalPayment).Snapshot();
        return Ok(new
        {
            logicalPayment = logicalPayment.ToString("N"),
            orderId = order?.Id,
            paymentStatus = order?.PaymentStatus.ToString() ?? "Missing",
            orderStatus = order?.OrderStatus.ToString() ?? "Missing",
            webhookReceivedCount = snapshot.WebhookReceivedCount,
            webhookAppliedCount = snapshot.WebhookAppliedCount,
            webhookSignatureFailureCount = snapshot.WebhookSignatureFailureCount,
            orderPaidEventCount = snapshot.OrderPaidEventCount,
            webhookEventTypes = snapshot.WebhookEventTypes,
        });
    }

    private async Task<Order> EnsureOrderAsync(Guid logicalPayment, decimal orderTotal, CancellationToken cancellationToken)
    {
        ReferenceState.PaymentState paymentState = referenceState.Get(logicalPayment);
        await paymentState.OrderCreationGate.WaitAsync(cancellationToken);
        try
        {
            Order? existing = await orderService.GetOrderByGuidAsync(logicalPayment);
            if (existing is not null) return existing;

            var store = await storeContext.GetCurrentStoreAsync();
            var customer = await customerService.GetCustomerByEmailAsync(ReferenceAdminEmail)
                ?? throw new InvalidOperationException("Reference customer is missing.");
            var language = (await languageService.GetAllLanguagesAsync(showHidden: true, storeId: store.Id)).FirstOrDefault()
                ?? throw new InvalidOperationException("Reference language is missing.");

            var billingAddress = new Address
            {
                FirstName = "Paytness",
                LastName = "Reference",
                Email = ReferenceAdminEmail,
                City = "Test",
                Address1 = "Reference",
                ZipPostalCode = "00000",
                PhoneNumber = "0000000",
                CreatedOnUtc = DateTime.UtcNow,
            };
            await addressService.InsertAddressAsync(billingAddress);

            var order = new Order
            {
                OrderGuid = logicalPayment,
                StoreId = store.Id,
                CustomerId = customer.Id,
                BillingAddressId = billingAddress.Id,
                PickupInStore = false,
                OrderStatus = OrderStatus.Pending,
                ShippingStatus = ShippingStatus.ShippingNotRequired,
                PaymentStatus = PaymentStatus.Pending,
                PaymentMethodSystemName = ReferencePaymentMethod.SystemName,
                CustomerCurrencyCode = "USD",
                CurrencyRate = 1m,
                CustomerTaxDisplayType = TaxDisplayType.ExcludingTax,
                OrderSubtotalInclTax = orderTotal,
                OrderSubtotalExclTax = orderTotal,
                OrderSubTotalDiscountInclTax = 0m,
                OrderSubTotalDiscountExclTax = 0m,
                OrderShippingInclTax = 0m,
                OrderShippingExclTax = 0m,
                PaymentMethodAdditionalFeeInclTax = 0m,
                PaymentMethodAdditionalFeeExclTax = 0m,
                TaxRates = string.Empty,
                OrderTax = 0m,
                OrderDiscount = 0m,
                OrderTotal = orderTotal,
                RefundedAmount = 0m,
                CheckoutAttributeDescription = string.Empty,
                CheckoutAttributesXml = string.Empty,
                CustomerLanguageId = language.Id,
                AffiliateId = 0,
                CustomerIp = "127.0.0.1",
                AllowStoringCreditCardNumber = false,
                Deleted = false,
                CreatedOnUtc = DateTime.UtcNow,
                CustomOrderNumber = $"PT-{logicalPayment:N}",
            };
            await orderService.InsertOrderAsync(order);
            return order;
        }
        finally
        {
            paymentState.OrderCreationGate.Release();
        }
    }

}
