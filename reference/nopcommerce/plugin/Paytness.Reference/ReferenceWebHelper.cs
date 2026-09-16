using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Hosting;
using Nop.Core;

namespace Paytness.Reference;

internal sealed class ReferenceWebHelper : WebHelper
{
    public ReferenceWebHelper(
        IActionContextAccessor actionContextAccessor,
        IHostApplicationLifetime hostApplicationLifetime,
        IHttpContextAccessor httpContextAccessor,
        IUrlHelperFactory urlHelperFactory,
        Lazy<IStoreContext> storeContext)
        : base(actionContextAccessor, hostApplicationLifetime, httpContextAccessor, urlHelperFactory, storeContext)
    {
    }

    public override string GetStoreLocation(bool? useSsl = null)
    {
        if (_httpContextAccessor.HttpContext is null && ReferenceMode.Enabled)
            return ReferenceMode.StoreOrigin.EndsWith('/')
                ? ReferenceMode.StoreOrigin
                : ReferenceMode.StoreOrigin + "/";

        return base.GetStoreLocation(useSsl);
    }
}
