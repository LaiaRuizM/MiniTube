using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniTube.Data;

namespace MiniTube.Tests;

/// <summary>
/// Spins up the real MiniTube app but swaps Azure SQL for an in-memory DB
/// and replaces Google OAuth with a fake "Test" auth scheme.
/// </summary>
public class MiniTubeWebFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Inject fake Google OAuth credentials so the app's AddGoogle(...)
        // registration passes option validation. In real environments these
        // come from appsettings.Development.json (gitignored), so CI would
        // otherwise fail at startup with "ClientId option must be provided".
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Google:ClientId"] = "test-client-id",
                ["Google:ClientSecret"] = "test-client-secret",
            });
        });

        builder.ConfigureServices(services =>
        {
            // ── Replace Azure SQL with EF Core InMemory ─────────────────
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<MiniTubeDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            services.AddDbContext<MiniTubeDbContext>(options =>
                options.UseInMemoryDatabase("IntegrationTests"));
        });

        // ConfigureTestServices runs AFTER the app's own service registration,
        // so it can override the auth schemes that Program.cs registered.
        builder.ConfigureTestServices(services =>
        {
            // Override all authentication with our fake test handler
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
                options.DefaultScheme = "Test";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        });

        builder.UseEnvironment("Development");
    }
}
