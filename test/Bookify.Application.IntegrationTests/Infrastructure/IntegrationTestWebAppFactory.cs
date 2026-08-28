using System.Net.Http.Json;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Payments;
using Bookify.Application.IntegrationTests.Users;
using Bookify.Api.Controllers.Users.Requests;
using Bookify.Application.Options;
using Bookify.Infrastructure;
using Bookify.Infrastructure.Authentication;
using Bookify.Infrastructure.Data;
using Bookify.Infrastructure.Outbox;
using Bookify.Infrastructure.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz;
using Testcontainers.Azurite;
using Testcontainers.Keycloak;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Testcontainers.ServiceBus;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;

namespace Bookify.Application.IntegrationTests.Infrastructure;

public class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly INetwork _network = new NetworkBuilder().Build();

    private readonly PostgreSqlContainer _dbContainer;

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

    private readonly ServiceBusContainer _serviceBusContainer;

    private readonly AzuriteContainer _storageContainer;

    private IFutureDockerImage _functionImage = default!;
    private IContainer _functionContainer = default!;

    public IntegrationTestWebAppFactory()
    {
        _dbContainer = new PostgreSqlBuilder()
            .WithImage("postgres:17")
            .WithDatabase("bookify")
            .WithUsername("postgres")
            .WithPassword("postgrespw")
            .WithNetwork(_network)
            .WithNetworkAliases("bookify-db")
            .Build();

        _serviceBusContainer = new ServiceBusBuilder()
            .WithAcceptLicenseAgreement(true)
            .WithResourceMapping(
                new FileInfo(".files/Config.json"),
                "/ServiceBus_Emulator/ConfigFiles")
            .WithNetwork(_network)
            .WithNetworkAliases("servicebus-emulator")
            .Build();

        _storageContainer = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .WithNetwork(_network)
            .WithNetworkAliases("storage")
            .Build();
    }

    public MockEmailService MockEmailService { get; } = new();

    public MockPaymentGateway MockPaymentGateway { get; } = new();

    public MockStripeCustomerService MockStripeCustomerService { get; } = new();

    public TestDateTimeProvider TestDateTimeProvider { get; } = new();


    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("ConnectionStrings:Database", _dbContainer.GetConnectionString());
        builder.UseSetting("ConnectionStrings:ServiceBus", _serviceBusContainer.GetConnectionString());
        builder.UseSetting("AccountDeletion:GracePeriodHours", "0.00083");

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
            services.PostConfigure<BookingOptions>(options =>
            {
                options.CheckoutSessionTtlMinutes = 0.05; // 3 seconds
                options.HostApprovalTtlHours = 0.00083; // 3 seconds
            });

            // Configure Account Deletion grace period to be extremely short for integration tests (3 seconds)
            services.PostConfigure<AccountDeletionOptions>(options =>
            {
                options.GracePeriodHours = 0.00083; // 3 seconds
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

            services.RemoveAll<Bookify.Application.Abstractions.Clock.IDateTimeProvider>();
            services.AddSingleton<Bookify.Application.Abstractions.Clock.IDateTimeProvider>(TestDateTimeProvider);

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

            services.Configure<InvoicesBlobStorageOptions>(options =>
            {
                options.ConnectionString = _storageContainer.GetConnectionString();
                options.ContainerName = "invoices";
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _network.CreateAsync().ConfigureAwait(false);

        _functionImage = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(CommonDirectoryPath.GetSolutionDirectory().DirectoryPath)
            .WithDockerfile(Path.Combine("src", "Bookify.Functions", "Dockerfile"))
            .WithName("bookify-functions:test")
            .WithCleanUp(false)
            .Build();

        await Task.WhenAll(
            _dbContainer.StartAsync(),
            _redisContainer.StartAsync(),
            _keycloakContainer.StartAsync(),
            _serviceBusContainer.StartAsync(),
            _storageContainer.StartAsync(),
            _functionImage.CreateAsync()).ConfigureAwait(false);

        _functionContainer = new ContainerBuilder()
            .WithImage(_functionImage)
            .WithNetwork(_network)
            .WithNetworkAliases("bookify-function")
            .WithEnvironment("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated")
            .WithEnvironment("AzureWebJobsStorage", BuildInternalAzuriteConnectionString())
            .WithEnvironment("BlobStorage__ConnectionString", BuildInternalAzuriteConnectionString())
            .WithEnvironment("BlobStorage__ContainerName", "invoices")
            .WithEnvironment("ServiceBusConnection", BuildInternalServiceBusConnectionString())
            .WithEnvironment("ConnectionStrings__Database", BuildInternalDbConnectionString())
            .WithEnvironment("AzureFunctionsJobHost__Logging__Console__IsEnabled", "true")
            .WithEnvironment("AzureFunctionsJobHost__Logging__LogLevel__Default", "Information")
            .WithPortBinding(80, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPath("/api/health").ForPort(80)))
            .Build();

        await _functionContainer.StartAsync().ConfigureAwait(false);

        await InitializeTestUserAsync().ConfigureAwait(false);
    }

    // We decorate DisposeAsync with a 'new' keyword because the WebApplicationFactory already implements IAsyncLifetime
    public new async Task DisposeAsync()
    {
        await base.DisposeAsync().ConfigureAwait(false);

        await Task.WhenAll(
            _functionContainer.StopAsync(),
            _storageContainer.StopAsync(),
            _dbContainer.StopAsync(),
            _redisContainer.StopAsync(),
            _keycloakContainer.StopAsync(),
            _serviceBusContainer.StopAsync()).ConfigureAwait(false);

        await Task.WhenAll(
            _functionContainer.DisposeAsync().AsTask(),
            _functionImage.DisposeAsync().AsTask(),
            _storageContainer.DisposeAsync().AsTask(),
            _dbContainer.DisposeAsync().AsTask(),
            _redisContainer.DisposeAsync().AsTask(),
            _keycloakContainer.DisposeAsync().AsTask(),
            _serviceBusContainer.DisposeAsync().AsTask()).ConfigureAwait(false);

        await _network.DisposeAsync().ConfigureAwait(false);
    }

    public async Task<(string Stdout, string Stderr)> GetFunctionLogsAsync()
    {
        return await _functionContainer.GetLogsAsync().ConfigureAwait(false);
    }

    private static string BuildInternalAzuriteConnectionString() =>
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://storage:10000/devstoreaccount1;QueueEndpoint=http://storage:10001/devstoreaccount1;TableEndpoint=http://storage:10002/devstoreaccount1;";

    private static string BuildInternalServiceBusConnectionString() =>
        "Endpoint=sb://servicebus-emulator;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    private static string BuildInternalDbConnectionString() =>
        "Host=bookify-db;Port=5432;Database=bookify;Username=postgres;Password=postgrespw";

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
                await httpClient.PostAsJsonAsync("api/v1/users/register/guest", request).ConfigureAwait(false);
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
