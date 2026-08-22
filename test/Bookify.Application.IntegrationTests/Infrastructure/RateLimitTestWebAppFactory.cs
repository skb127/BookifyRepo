using System.Net.Http.Json;
using System.Threading.RateLimiting;
using Bookify.Api.Controllers.Users.Requests;
using Bookify.Application.Abstractions.Email;
using Bookify.Infrastructure.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.Keycloak;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Testcontainers.ServiceBus;
using DotNet.Testcontainers.Builders;

namespace Bookify.Application.IntegrationTests.Infrastructure;

public class RateLimitTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:17")
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
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilHttpRequestIsSucceeded(r => r.ForPath("/realms/bookify").ForPort(8080)))
        .Build();

    private readonly ServiceBusContainer _serviceBusContainer = new ServiceBusBuilder()
        .WithAcceptLicenseAgreement(true)
        .WithResourceMapping(
            new FileInfo(".files/Config.json"),
            "/ServiceBus_Emulator/ConfigFiles")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("ConnectionStrings:ServiceBus", _serviceBusContainer.GetConnectionString());

        builder.ConfigureTestServices(services =>
        {
            // Re-configure DbContext
            services.RemoveAll<DbContextOptions<Bookify.Infrastructure.ApplicationDbContext>>();
            services.AddDbContext<Bookify.Infrastructure.ApplicationDbContext>(options =>
                options.UseNpgsql(_dbContainer.GetConnectionString()).UseSnakeCaseNamingConvention());

            services.RemoveAll<Bookify.Application.Abstractions.Data.ISqlConnectionFactory>();

            services.AddSingleton<Bookify.Application.Abstractions.Data.ISqlConnectionFactory>(_ =>
                new Bookify.Infrastructure.Data.SqlConnectionFactory(_dbContainer.GetConnectionString()));

            // Speed up Outbox processing for integration tests
            services.Configure<Bookify.Infrastructure.Outbox.OutboxOptions>(o =>
            {
                o.IntervalInSeconds = 1;
                o.BatchSize = 1000;
            });

            // Speed up CompleteBookings processing for integration tests (default is daily)
            services.Configure<Bookify.Infrastructure.Bookings.CompleteBookingsJobOptions>(o =>
                o.CronExpression = "*/2 * * * * ?"); // Every two seconds

            services.Configure<Options.ExpirationOptions>(options =>
            {
                options.EmailChangeExpirationSeconds = 20;
                options.PasswordRecoveryExpirationSeconds = 20;
            });

            // Re-configure Redis
            services.RemoveAll<Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions>();
            services.AddStackExchangeRedisCache(redisCacheOptions =>
                redisCacheOptions.Configuration = _redisContainer.GetConnectionString());

            // Re-configure Keycloak authentication
            var keycloakAddress = _keycloakContainer.GetBaseAddress();
            services.Configure<KeycloakOptions>(options =>
            {
                options.AdminUrl = new Uri($"{keycloakAddress}admin/realms/bookify/");
                options.TokenUrl = new Uri($"{keycloakAddress}realms/bookify/protocol/openid-connect/token");
                options.OidcBaseUrl = new Uri($"{keycloakAddress}realms/bookify/protocol/openid-connect/");
                options.BaseUrl = new Uri(keycloakAddress);
            });
            services.Configure<AuthenticationOptions>(options =>
            {
                options.Issuer = $"{keycloakAddress}realms/bookify/";
                options.MetadataUrl = new Uri($"{keycloakAddress}realms/bookify/.well-known/openid-configuration");
            });

            services.AddSingleton<IEmailService>(new MockEmailService());

            // Rate Limiting Override for Tests
            services.RemoveAll<Microsoft.Extensions.Options.IConfigureOptions<RateLimiterOptions>>();
            services.AddRateLimiter(options =>
            {
                options.AddPolicy("write-operations", ctx =>
                {
                    if (ctx.Request.Headers.ContainsKey("X-Test-Bypass-RateLimit"))
                    {
                        return RateLimitPartition.GetNoLimiter("bypass");
                    }

                    string key = GetClientKey(ctx);
                    return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
                        new FixedWindowRateLimiterOptions
                            { PermitLimit = 2, Window = TimeSpan.FromSeconds(15), QueueLimit = 0 });
                });

                options.AddPolicy("search", ctx =>
                {
                    if (ctx.Request.Headers.ContainsKey("X-Test-Bypass-RateLimit"))
                    {
                        return RateLimitPartition.GetNoLimiter("bypass");
                    }

                    string key = GetClientKey(ctx);
                    return RateLimitPartition.GetSlidingWindowLimiter(key, _ =>
                        new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = 2, Window = TimeSpan.FromSeconds(15), SegmentsPerWindow = 2, QueueLimit = 0
                        });
                });

                options.AddPolicy("health-checks", ctx =>
                {
                    if (ctx.Request.Headers.ContainsKey("X-Test-Bypass-RateLimit"))
                    {
                        return RateLimitPartition.GetNoLimiter("bypass");
                    }

                    string key = GetClientKey(ctx);
                    return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
                        new FixedWindowRateLimiterOptions
                            { PermitLimit = 2, Window = TimeSpan.FromSeconds(15), QueueLimit = 0 });
                });

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                {
                    if (ctx.Request.Headers.ContainsKey("X-Test-Bypass-RateLimit"))
                    {
                        return RateLimitPartition.GetNoLimiter("bypass");
                    }

                    string key = GetClientKey(ctx);
                    return RateLimitPartition.GetSlidingWindowLimiter(key, _ =>
                        new SlidingWindowRateLimiterOptions
                            { PermitLimit = 3, Window = TimeSpan.FromSeconds(15), SegmentsPerWindow = 2 });
                });

                options.OnRejected = async (context, token) =>
                {
                    context.HttpContext.Response.StatusCode = 429;
                    await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
                    {
                        Status = 429,
                        Title = "Too Many Requests",
                        Detail = "You have exceeded the request limit. Please try again later."
                    }, token).ConfigureAwait(false);
                };
            });

            services.Configure<Bookify.Infrastructure.Security.TurnstileOptions>(options =>
            {
                options.BaseUrl = new Uri("https://challenges.cloudflare.com/turnstile/v0/");
                options.SecretKey = "1x0000000000000000000000000000000AA";
            });

            // Remove scheduler to avoid concurrency issues in integration tests
            // Use in-memory scheduler for testing
            services.Configure<Quartz.QuartzOptions>(options =>
            {
                options.Remove("quartz.jobStore.tablePrefix");
                options.Remove("quartz.jobStore.useProperties");
                options.Remove("quartz.jobStore.dataSource");
                options.Remove("quartz.jobStore.driverDelegateType");
                options.Remove("quartz.jobStore.serializer.type");

                options["quartz.jobStore.type"] = "Quartz.Simpl.RAMJobStore, Quartz";
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync().ConfigureAwait(false);
        await _redisContainer.StartAsync().ConfigureAwait(false);
        await _keycloakContainer.StartAsync().ConfigureAwait(false);
        await _serviceBusContainer.StartAsync().ConfigureAwait(false);
        await RegisterTestUsersAsync().ConfigureAwait(false);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync().ConfigureAwait(false);

        await _dbContainer.StopAsync().ConfigureAwait(false);
        await _redisContainer.StopAsync().ConfigureAwait(false);
        await _keycloakContainer.StopAsync().ConfigureAwait(false);
        await _serviceBusContainer.StopAsync().ConfigureAwait(false);
        await _dbContainer.DisposeAsync().ConfigureAwait(false);
        await _redisContainer.DisposeAsync().ConfigureAwait(false);
        await _keycloakContainer.DisposeAsync().ConfigureAwait(false);
        await _serviceBusContainer.DisposeAsync().ConfigureAwait(false);
    }

    private async Task RegisterTestUsersAsync()
    {
        async Task Register(RegisterUserRequest request)
        {
            using HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Add("X-Test-Bypass-RateLimit", "true");
            client.DefaultRequestHeaders.Add("X-Turnstile-Token", "XXXX.DUMMY.TOKEN.XXXX");

            HttpResponseMessage response =
                await client.PostAsJsonAsync("api/v1/users/register/guest", request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        await Register(RateLimitUserData.WriteOpsUser).ConfigureAwait(false);
        await Register(RateLimitUserData.SearchUser).ConfigureAwait(false);
        await Register(RateLimitUserData.GlobalLimiterUserA).ConfigureAwait(false);
        await Register(RateLimitUserData.GlobalLimiterUserB).ConfigureAwait(false);
    }

    private static string GetClientKey(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            return context.User.GetIdentityId();
        }

        if (context.Request.Headers.TryGetValue("X-Test-IP", out var ip))
        {
            return ip.ToString();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

public static class RateLimitUserData
{
    public static readonly RegisterUserRequest WriteOpsUser = new("write.ops@test.com", "Test", "User", "Password123!",
        new DateOnly(2000, 1, 1));

    public static readonly RegisterUserRequest SearchUser = new("search.ops@test.com", "Test", "User", "Password123!",
        new DateOnly(2000, 1, 1));

    public static readonly RegisterUserRequest GlobalLimiterUserA =
        new("global.a@test.com", "Test", "User", "Password123!", new DateOnly(2000, 1, 1));

    public static readonly RegisterUserRequest GlobalLimiterUserB =
        new("global.b@test.com", "Test", "User", "Password123!", new DateOnly(2000, 1, 1));
}
