using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Paytness.Reference;

public sealed class ReferencePaymentInfoViewComponent : NopViewComponent
{
    public IViewComponentResult Invoke() => Content("Paytness reference payment (test only).");
}
