using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Paytness.Reference;

public sealed class ReferenceRouteProvider : IRouteProvider
{
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        if (!ReferenceMode.Enabled) return;

        endpointRouteBuilder.MapControllerRoute(
            "Paytness.Reference.Pay",
            "test/pay",
            new { controller = "PaytnessReference", action = "Pay" });
        endpointRouteBuilder.MapControllerRoute(
            "Paytness.Reference.Webhook",
            "test/webhook",
            new { controller = "PaytnessReferenceWebhook", action = "Webhook" });
        endpointRouteBuilder.MapControllerRoute(
            "Paytness.Reference.State",
            "test/state",
            new { controller = "PaytnessReference", action = "State" });
    }

    public int Priority => 1000;
}
