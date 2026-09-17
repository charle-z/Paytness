using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

internal sealed class ProbeSut : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly object _gate = new();
    private string _paymentStatus = "Pending";
    private int _paidCount;

    private ProbeSut(WebApplication app, Uri origin)
    {
        _app = app;
        Origin = origin;
    }

    public Uri Origin { get; }
    public Uri ProviderOrigin { get; set; } = new("http://127.0.0.1:8787");
    public TaskCompletionSource BlockingWebhookStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource NotReadyObservationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Reset()
    {
        lock (_gate)
        {
            _paymentStatus = "Pending";
            _paidCount = 0;
        }
    }

    public static async Task<ProbeSut> StartAsync(CancellationToken cancellationToken = default)
    {
        int port = GetFreePort();
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, port));
        WebApplication app = builder.Build();
        var sut = new ProbeSut(app, new Uri($"http://127.0.0.1:{port}"));

        app.MapPost("/test/pay", async context =>
        {
            using var client = new HttpClient { BaseAddress = sut.ProviderOrigin, Timeout = Timeout.InfiniteTimeSpan };
            using var request = new HttpRequestMessage(HttpMethod.Post, "/provider/v1/payments")
            {
                Content = JsonContent.Create(new { merchantReference = "probe-order", amountMinor = 1250, currency = "USD" }),
            };
            request.Headers.TryAddWithoutValidation("Idempotency-Key", "probe-order");
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            if (!response.IsSuccessStatusCode)
            {
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
                return;
            }
            lock (sut._gate)
            {
                if (sut._paymentStatus != "Paid")
                {
                    sut._paymentStatus = "Paid";
                    sut._paidCount++;
                }
            }
            await context.Response.WriteAsJsonAsync(new { status = "ok" }, context.RequestAborted);
        });

        app.MapPost("/test/webhook-blocking", async context =>
        {
            sut.BlockingWebhookStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
        });

        app.MapGet("/test/not-ready", () =>
        {
            sut.NotReadyObservationStarted.TrySetResult();
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        });

        app.MapGet("/test/state", () =>
        {
            lock (sut._gate)
                return Results.Ok(new { paymentStatus = sut._paymentStatus, orderPaidEventCount = sut._paidCount });
        });

        await app.StartAsync(cancellationToken);
        return sut;
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _app.StopAsync(stop.Token);
        await _app.DisposeAsync();
    }
}
