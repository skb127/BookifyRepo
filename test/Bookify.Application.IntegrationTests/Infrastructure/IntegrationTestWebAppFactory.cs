using System.Net.Http.Json;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Email;
using Bookify.Application.IntegrationTests.Users;
using Bookify.Application.Options;
using Bookify.Infrastructure;
using Bookify.Infrastructure.Authentication;
using Bookify.Infrastructure.Data;
using Bookify.Infrastructure.Outbox;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.Keycloak;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Bookify.Application.IntegrationTests.Infrastructure;

public class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
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
        .Build();

    public MockEmailService MockEmailService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(_dbContainer.GetConnectionString())
                    .UseSnakeCaseNamingConvention());

            services.RemoveAll<ISqlConnectionFactory>();

            services.AddSingleton<ISqlConnectionFactory>(_ =>
                new SqlConnectionFactory(_dbContainer.GetConnectionString()));

            services.Configure<RedisCacheOptions>(redisCacheOptions =>
                redisCacheOptions.Configuration = _redisContainer.GetConnectionString());

            // Speed up Outbox processing for integration tests
            services.Configure<OutboxOptions>(o =>
            {
                o.IntervalInSeconds = 1;
                o.BatchSize = 1000; 
            });

            // Speed up CompleteBookings processing for integration tests (default is daily)
            services.Configure<Bookify.Infrastructure.Bookings.CompleteBookingsJobOptions>(o =>
                o.CronExpression = "*/2 * * * * ?"); // Every two seconds

            // Configure NotifyCompletedBookings job for integration tests.
            // Uses a long interval to prevent interference with existing tests.
            // Tests that specifically need this job can override this configuration.
            services.Configure<Bookify.Infrastructure.Bookings.NotifyCompletedBookingsJobOptions>(o =>
            {
                o.CronExpression = "0 0 0 1 1 ? 2099"; // Effectively disabled —  1st January 2099
                o.BatchSize = 10;
            });

            services.Configure<ExpirationOptions>(options =>
            {
                options.EmailChangeExpirationSeconds = 20;
                options.PasswordRecoveryExpirationSeconds = 20;
            });

            string? keycloakAddress = _keycloakContainer.GetBaseAddress();

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

            services.AddSingleton<IEmailService>(MockEmailService);

            // Bypass RateLimiting for all old integration tests
            services.RemoveAll<Microsoft.Extensions.Options.IConfigureOptions<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>>();
            services.AddRateLimiter(options =>
            {
                options.AddPolicy("write-operations", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("bypass"));
                options.AddPolicy("search", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("bypass"));
                options.AddPolicy("health-checks", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("bypass"));
                options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<Microsoft.AspNetCore.Http.HttpContext, string>(
                    _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("bypass"));
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
        using HttpClient httpClient = CreateClient();
        httpClient.DefaultRequestHeaders.Add("X-Turnstile-Token", "XXXX.DUMMY.TOKEN.XXXX");

        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.RegisterTestUserRequest)
            .ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.LoginUserRequest).ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.RefreshTokenUserRequest)
            .ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.ExistingUserRequest).ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.LogoutTestUserRequest).ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.ChangePasswordUserRequest)
            .ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.ChangePasswordUserRequest2)
            .ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.ChangeEmailUserRequest)
            .ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.ChangeEmailUserRequest2)
            .ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.ChangeEmailPendingUserRequest)
            .ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.PasswordRecoveryUserRequest)
            .ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.PasswordResetUserRequest)
            .ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.UpdateProfileUserRequest)
            .ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.UpdateProfileUserRequest2)
            .ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.GetUserByIdUserRequest)
            .ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.RevokeSessionsUserRequest)
            .ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.CreateApartmentStandardUserRequest)
            .ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.CreateApartmentAdminUserRequest)
            .ConfigureAwait(false);


        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.UpdateApartmentStandardUserRequest)
            .ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.UpdateApartmentAdminUserRequest)
            .ConfigureAwait(false);


        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.DeleteApartmentStandardUserRequest)
            .ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.DeleteApartmentAdminUserRequest)
            .ConfigureAwait(false);


        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.UpdateReviewSecondaryUserRequest)
            .ConfigureAwait(false);

        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.DeleteReviewSecondaryUserRequest)
            .ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.DeleteReviewTertiaryUserRequest)
            .ConfigureAwait(false);

        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.GetAllReviewsAdminUserRequest)
            .ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.GetAllReviewsRegularUserRequest)
            .ConfigureAwait(false);

        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.CacheInvalidationAdminUserRequest)
            .ConfigureAwait(false);
        await httpClient.PostAsJsonAsync("api/v1/users/register", UserData.CacheInvalidationUserRequest)
            .ConfigureAwait(false);
    }
}
