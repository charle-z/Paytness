using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Nop.Web.Framework.Components;

namespace Paytness.Reference;

public sealed class ReferencePaymentInfoViewComponent : NopViewComponent
{
    private static readonly HtmlString ReferenceNotice = new(
        "Paytness reference payment (test only). <a href=\"https://www.nopcommerce.com\">powered by nopCommerce</a>");

    public IViewComponentResult Invoke() => new HtmlContentViewComponentResult(ReferenceNotice);
}
