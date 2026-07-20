using System.Net.Http.Json;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Payments;
using Bookify.Application.IntegrationTests.Users;
using Bookify.Api.Controllers.Users;
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
using Quartz;
using Testcontainers.Keycloak;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Testcontainers.ServiceBus;
using DotNet.Testcontainers.Builders;

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
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilHttpRequestIsSucceeded(r => r.ForPath("/realms/bookify").ForPort(8080)))
        .Build();

    private readonly ServiceBusContainer _serviceBusContainer = new ServiceBusBuilder()
        .WithAcceptLicenseAgreement(true)
        .WithResourceMapping(
            new FileInfo(".files/Config.json"),
            "/ServiceBus_Emulator/ConfigFiles")
        .Build();

    public MockEmailService MockEmailService { get; } = new();

    public MockPaymentGateway MockPaymentGateway { get; } = new();

    public MockStripeCustomerService MockStripeCustomerService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("ConnectionStrings:Database", _dbContainer.GetConnectionString());
        builder.UseSetting("ConnectionStrings:ServiceBus", _serviceBusContainer.GetConnectionString());

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

            // Speed up Outbox processing for integration tests
            services.Configure<OutboxOptions>(o =>
            {
                o.IntervalInSeconds = 1;
                o.BatchSize = 1000;
            });

            // Speed up CompleteBookings processing for integration tests (default is daily)
            services.Configure<Bookify.Infrastructure.Bookings.CompleteBookingsJobOptions>(o =>
                o.CronExpression = "*/2 * * * * ?"); // Every two seconds

            // Configure booking TTL options to be extremely short for integration tests
            services.Configure<BookingOptions>(options =>
            {
                options.CheckoutSessionTtlMinutes = 0.05; // 3 seconds
                options.HostApprovalTtlHours = 0.00083; // 3 seconds
            });

            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(MockPaymentGateway);

            services.RemoveAll<IStripeCustomerService>();
            services.AddSingleton<IStripeCustomerService>(MockStripeCustomerService);

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
            services
                .RemoveAll<Microsoft.Extensions.Options.IConfigureOptions<
                    Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>>();
            services.AddRateLimiter(options =>
            {
                options.AddPolicy("write-operations",
                    _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("bypass"));
                options.AddPolicy("search",
                    _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("bypass"));
                options.AddPolicy("health-checks",
                    _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("bypass"));
                options.GlobalLimiter =
                    System.Threading.RateLimiting.PartitionedRateLimiter
                        .Create<Microsoft.AspNetCore.Http.HttpContext, string>(_ =>
                            System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("bypass"));
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
        await _serviceBusContainer.StartAsync().ConfigureAwait(false);

        await InitializeTestUserAsync().ConfigureAwait(false);
    }

    // We decorate DisposeAsync with a 'new' keyword because the WebApplicationFactory already implements IAsyncLifetime
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

    /// <summary>
    /// Initialize a test user in the Keycloak server
    /// </summary>
    /// <returns></returns>
    private async Task InitializeTestUserAsync()
    {
        async Task Register(RegisterUserRequest request)
        {
            using HttpClient httpClient = CreateClient();
            httpClient.DefaultRequestHeaders.Add("X-Turnstile-Token", "XXXX.DUMMY.TOKEN.XXXX");

            HttpResponseMessage response =
                await httpClient.PostAsJsonAsync("api/v1/users/register", request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        await Register(UserData.RegisterTestUserRequest).ConfigureAwait(false);
        await Register(UserData.LoginUserRequest).ConfigureAwait(false);
        await Register(UserData.RefreshTokenUserRequest).ConfigureAwait(false);
        await Register(UserData.ExistingUserRequest).ConfigureAwait(false);
        await Register(UserData.LogoutTestUserRequest).ConfigureAwait(false);
        await Register(UserData.ChangePasswordUserRequest).ConfigureAwait(false);
        await Register(UserData.ChangePasswordUserRequest2).ConfigureAwait(false);
        await Register(UserData.ChangeEmailUserRequest).ConfigureAwait(false);
        await Register(UserData.ChangeEmailUserRequest2).ConfigureAwait(false);
        await Register(UserData.ChangeEmailPendingUserRequest).ConfigureAwait(false);
        await Register(UserData.PasswordRecoveryUserRequest).ConfigureAwait(false);
        await Register(UserData.PasswordResetUserRequest).ConfigureAwait(false);
        await Register(UserData.UpdateProfileUserRequest).ConfigureAwait(false);
        await Register(UserData.UpdateProfileUserRequest2).ConfigureAwait(false);
        await Register(UserData.GetUserByIdUserRequest).ConfigureAwait(false);
        await Register(UserData.RevokeSessionsUserRequest).ConfigureAwait(false);
        await Register(UserData.CreateApartmentStandardUserRequest).ConfigureAwait(false);
        await Register(UserData.CreateApartmentAdminUserRequest).ConfigureAwait(false);

        await Register(UserData.UpdateApartmentStandardUserRequest).ConfigureAwait(false);
        await Register(UserData.UpdateApartmentAdminUserRequest).ConfigureAwait(false);

        await Register(UserData.DeleteApartmentStandardUserRequest).ConfigureAwait(false);
        await Register(UserData.DeleteApartmentAdminUserRequest).ConfigureAwait(false);

        await Register(UserData.UpdateReviewSecondaryUserRequest).ConfigureAwait(false);

        await Register(UserData.DeleteReviewSecondaryUserRequest).ConfigureAwait(false);
        await Register(UserData.DeleteReviewTertiaryUserRequest).ConfigureAwait(false);

        await Register(UserData.GetAllReviewsAdminUserRequest).ConfigureAwait(false);
        await Register(UserData.GetAllReviewsRegularUserRequest).ConfigureAwait(false);

        await Register(UserData.CacheInvalidationAdminUserRequest).ConfigureAwait(false);
        await Register(UserData.CacheInvalidationUserRequest).ConfigureAwait(false);
    }
}
