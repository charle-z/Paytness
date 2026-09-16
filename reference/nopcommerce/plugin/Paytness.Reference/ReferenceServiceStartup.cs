using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nop.Core;
using Nop.Core.Infrastructure;

namespace Paytness.Reference;

public sealed class ReferenceServiceStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        if (!ReferenceMode.Enabled) return;
        services.Replace(ServiceDescriptor.Scoped<IWebHelper, ReferenceWebHelper>());
        services.AddSingleton<ReferenceState>();
    }

    public void Configure(IApplicationBuilder application)
    {
    }

    public int Order => 3_000;
}
