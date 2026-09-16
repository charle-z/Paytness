using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Domain.Stores;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Services.Installation;

namespace Paytness.Reference;

public sealed class ReferenceStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void Configure(IApplicationBuilder application)
    {
        if (!ReferenceMode.Enabled) return;

        using IServiceScope scope = application.ApplicationServices.CreateScope();
        INopDataProvider dataProvider = scope.ServiceProvider.GetRequiredService<INopDataProvider>();
        if (dataProvider.GetTable<Store>().Any()) return;

        IInstallationService installationService = scope.ServiceProvider.GetRequiredService<IInstallationService>();
        string bootstrapCredential = CreateEphemeralCredential();
        try
        {
            installationService.InstallAsync(new InstallationSettings
            {
                AdminEmail = "paytness-reference@example.invalid",
                AdminPassword = bootstrapCredential,
                LanguagePackDownloadLink = string.Empty,
                LanguagePackProgress = 0,
                RegionInfo = new RegionInfo("US"),
                CultureInfo = new CultureInfo("en-US"),
                InstallSampleData = false,
            }).GetAwaiter().GetResult();
        }
        finally
        {
            bootstrapCredential = string.Empty;
        }
    }

    public int Order => 50;

    private static string CreateEphemeralCredential()
    {
        byte[] random = RandomNumberGenerator.GetBytes(32);
        try
        {
            return $"R{Convert.ToHexString(random)}aA1!";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(random);
        }
    }
}
