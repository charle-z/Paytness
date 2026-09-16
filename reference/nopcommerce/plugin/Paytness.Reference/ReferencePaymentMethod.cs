using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Services.Payments;
using Nop.Services.Plugins;

namespace Paytness.Reference;

public sealed class ReferencePaymentMethod(IHttpClientFactory httpClientFactory) : BasePlugin, IPaymentMethod
{
    public const string SystemName = "Paytness.Reference";
    public async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        ArgumentNullException.ThrowIfNull(processPaymentRequest);
        var result = new ProcessPaymentResult { NewPaymentStatus = PaymentStatus.Pending };
        if (!ReferenceMode.Enabled)
        {
            result.AddError("Paytness reference mode is disabled.");
            return result;
        }

        Uri providerOrigin;
        try
        {
            providerOrigin = ReferenceMode.GetProviderOrigin();
        }
        catch (InvalidOperationException exception)
        {
            result.AddError(exception.Message);
            return result;
        }

        string logicalPayment = processPaymentRequest.OrderGuid.ToString("N");
        string idempotencyKey = ReferenceMode.UnstableIdempotency
            ? Guid.NewGuid().ToString("N")
            : logicalPayment;
        long amountMinor = checked((long)decimal.Round(processPaymentRequest.OrderTotal * 100m, 0, MidpointRounding.AwayFromZero));

        using HttpClient client = httpClientFactory.CreateClient();
        client.BaseAddress = providerOrigin;
        client.Timeout = TimeSpan.FromSeconds(5);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/provider/v1/payments")
        {
            Content = JsonContent.Create(new
            {
                merchantReference = logicalPayment,
                amountMinor,
                currency = "USD",
            }),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                result.AddError($"Paytness provider returned HTTP {(int)response.StatusCode}.");
                return result;
            }

            ProviderPaymentResponse? provider = await response.Content.ReadFromJsonAsync<ProviderPaymentResponse>();
            if (provider is null || string.IsNullOrWhiteSpace(provider.ProviderAttemptId))
            {
                result.AddError("Paytness provider returned an invalid response.");
                return result;
            }

            result.CaptureTransactionId = provider.ProviderAttemptId;
            result.NewPaymentStatus = PaymentStatus.Paid;
            return result;
        }
        catch (HttpRequestException)
        {
            result.AddError("Paytness provider transport failure.");
            return result;
        }
        catch (TaskCanceledException)
        {
            result.AddError("Paytness provider timeout.");
            return result;
        }
    }

    public Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest) => Task.CompletedTask;
    public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart) => Task.FromResult(false);
    public Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart) => Task.FromResult(0m);
    public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest) => Task.FromResult(new CapturePaymentResult { Errors = ["Capture not supported."] });
    public Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest) => Task.FromResult(new RefundPaymentResult { Errors = ["Refund not supported."] });
    public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest) => Task.FromResult(new VoidPaymentResult { Errors = ["Void not supported."] });
    public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest) => Task.FromResult(new ProcessPaymentResult { Errors = ["Recurring payment not supported."] });
    public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest) => Task.FromResult(new CancelRecurringPaymentResult { Errors = ["Recurring payment not supported."] });
    public Task<bool> CanRePostProcessPaymentAsync(Order order) => Task.FromResult(false);
    public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form) => Task.FromResult<IList<string>>([]);
    public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form) => Task.FromResult(new ProcessPaymentRequest());
    public Type GetPublicViewComponent() => typeof(ReferencePaymentInfoViewComponent);
    public Task<string> GetPaymentMethodDescriptionAsync() => Task.FromResult("Paytness reference payment (test only).");

    public bool SupportCapture => false;
    public bool SupportPartiallyRefund => false;
    public bool SupportRefund => false;
    public bool SupportVoid => false;
    public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;
    public PaymentMethodType PaymentMethodType => PaymentMethodType.Standard;
    public bool SkipPaymentInfo => true;

    private sealed record ProviderPaymentResponse(string ProviderAttemptId, string LogicalPayment, string State);
}
