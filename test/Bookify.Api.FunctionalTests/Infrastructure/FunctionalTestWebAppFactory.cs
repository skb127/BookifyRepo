using System.Net.Http.Json;
using Bookify.Api.FunctionalTests.Users;
using Bookify.Application.Abstractions.Data;
using Bookify.Infrastructure;
using Bookify.Infrastructure.Authentication;
using Bookify.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz;
using Testcontainers.Keycloak;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using DotNet.Testcontainers.Builders;

namespace Bookify.Api.FunctionalTests.Infrastructure;

public class FunctionalTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:latest")
        .WithDatabase("bookify")
        .WithUsername("postgres")
        .WithPassword("postgrespw")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:latest")
        .Build();

    private readonly KeycloakContainer _keycloakContainer = new KeycloakBuilder()
        .WithResourceMapping(
            new FileInfo(".files/bookify-realm-export.json"),
            new FileInfo("/opt/keycloak/data/import/realm.json"))
        .WithCommand("--import-realm")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPath("/realms/bookify").ForPort(8080)))
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("ConnectionStrings:Database", _dbContainer.GetConnectionString());

        builder.ConfigureTestServices(services =>
        {
            // Remove scheduler to avoid concurrency issues in integration tests
            // Use in-memory scheduler for testing
            services.Configure<QuartzOptions>(options =>
            {
                options.Remove("quartz.jobStore.tablePrefix");
                options.Remove("quartz.jobStore.useProperties");
                options.Remove("quartz.jobStore.dataSource");
                options.Remove("quartz.jobStore.driverDelegateType");
                options.Remove("quartz.jobStore.serializer.type");

                options["quartz.jobStore.type"] = "Quartz.Simpl.RAMJobStore, Quartz";
            });

            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(_dbContainer.GetConnectionString())
                    .UseSnakeCaseNamingConvention());

            services.RemoveAll<ISqlConnectionFactory>();

            services.AddSingleton<ISqlConnectionFactory>(_ =>
                new SqlConnectionFactory(_dbContainer.GetConnectionString()));

            services.Configure<RedisCacheOptions>(redisCacheOptions =>
                redisCacheOptions.Configuration = _redisContainer.GetConnectionString());

            string? keycloakAddress = _keycloakContainer.GetBaseAddress();

            services.Configure<KeycloakOptions>(options =>
            {
                options.AdminUrl = new Uri($"{keycloakAddress}admin/realms/bookify/");
                options.TokenUrl = new Uri($"{keycloakAddress}realms/bookify/protocol/openid-connect/token");
                options.OidcBaseUrl = new Uri($"{keycloakAddress}realms/bookify/protocol/openid-connect/");
                // Override BaseUrl to prevent container hostname resolution issues in test environment
                options.BaseUrl = new Uri(keycloakAddress);
            });

            services.Configure<AuthenticationOptions>(options =>
            {
                options.Issuer = $"{keycloakAddress}realms/bookify/";
                options.MetadataUrl = new Uri($"{keycloakAddress}realms/bookify/.well-known/openid-configuration");
            });

            services.Configure<Bookify.Infrastructure.Security.TurnstileOptions>(options =>
            {
                options.BaseUrl = new Uri("https://challenges.cloudflare.com/turnstile/v0/");
                options.SecretKey = "1x0000000000000000000000000000000AA";
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync().ConfigureAwait(false);
        await _redisContainer.StartAsync().ConfigureAwait(false);
        await _keycloakContainer.StartAsync().ConfigureAwait(false);

        await InitializeTestUserAsync().ConfigureAwait(false);
    }

    // We decorate DisposeAsync with a 'new' keyword because the WebApplicationFactory already implements IAsyncLifetime
    public new async Task DisposeAsync()
    {
        await base.DisposeAsync().ConfigureAwait(false);

        await _dbContainer.StopAsync().ConfigureAwait(false);
        await _redisContainer.StopAsync().ConfigureAwait(false);
        await _keycloakContainer.StopAsync().ConfigureAwait(false);

        await _dbContainer.DisposeAsync().ConfigureAwait(false);
        await _redisContainer.DisposeAsync().ConfigureAwait(false);
        await _keycloakContainer.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Initialize a test user in the Keycloak server
    /// </summary>
    /// <returns></returns>
    private async Task InitializeTestUserAsync()
    {
        HttpClient httpClient = CreateClient();
        httpClient.DefaultRequestHeaders.Add("X-Turnstile-Token", "XXXX.DUMMY.TOKEN.XXXX");

        var response1 = await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.RegisterTestUserRequest).ConfigureAwait(false);
        response1.EnsureSuccessStatusCode();

        var response2 = await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.RegisterTestUserRequest2).ConfigureAwait(false);
        response2.EnsureSuccessStatusCode();

        var response3 = await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.RegisterTestUserRequest3).ConfigureAwait(false);
        response3.EnsureSuccessStatusCode();
    }
}
