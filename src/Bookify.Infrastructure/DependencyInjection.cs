using Asp.Versioning;
using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Caching;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Identity;
using Bookify.Application.Abstractions.Security;
using Bookify.Application.Options;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Reviews;
using Bookify.Domain.Users;
using Bookify.Infrastructure.Authentication;
using Bookify.Infrastructure.Authorization;
using Bookify.Infrastructure.Bookings;
using Bookify.Infrastructure.Caching;
using Bookify.Infrastructure.Clock;
using Bookify.Infrastructure.Data;
using Bookify.Infrastructure.Email;
using Bookify.Infrastructure.Identity;
using Bookify.Infrastructure.Outbox;
using Bookify.Infrastructure.RateLimiting;
using Bookify.Infrastructure.Repositories;
using Bookify.Infrastructure.Security;
using Dapper;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Quartz;
using AuthenticationOptions = Bookify.Infrastructure.Authentication.AuthenticationOptions;
using AuthenticationService = Bookify.Infrastructure.Authentication.AuthenticationService;
using IAuthenticationService = Bookify.Application.Abstractions.Authentication.IAuthenticationService;
using IAuthorizationService = Bookify.Application.Abstractions.Authorization.IAuthorizationService;
using RateLimitingOptions = Bookify.Infrastructure.RateLimiting.RateLimitingOptions;

namespace Bookify.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTransient<IDateTimeProvider, DateTimeProvider>();

        AddEmail(services, configuration);

        AddPersistence(services, configuration);

        AddAuthentication(services, configuration);

        AddIdentity(services);

        AddAuthorization(services);

        AddCaching(services, configuration);

        AddHealthChecks(services, configuration);

        AddApiVersioning(services);

        AddBackgroundJobs(services, configuration);

        AddTurnstile(services, configuration);

        AddOptions(services, configuration);

        AddRateLimiting(services, configuration);

        return services;
    }

    private static void AddEmail(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EmailOptions>(configuration.GetSection("Email"));

        services.AddSingleton<IEmailTemplateService, ScribanTemplateService>();

        services.AddTransient<IEmailService, SmtpEmailService>();

        services.AddTransient<ISmtpClient, SmtpClient>();
    }

    private static void AddAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.Configure<AuthenticationOptions>(configuration.GetSection("Authentication"));

        // JWT Bearer options
        services.ConfigureOptions<JwtBearerOptionsSetup>();

        // Configure Keycloak options
        services.Configure<KeycloakOptions>(configuration.GetSection("Keycloak"));

        services.AddTransient<AdminAuthorizationDelegatingHandler>();

        // Configure the delegating handler and AuthenticationService as a typed HTTP client
        services.AddHttpClient<IAuthenticationService, AuthenticationService>((sp, httpClient) =>
            {
                KeycloakOptions keycloakOptions = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;

                httpClient.BaseAddress = keycloakOptions.AdminUrl;
            })
            .AddHttpMessageHandler<AdminAuthorizationDelegatingHandler>();

        // Configure JwtService as typed HTTP client
        services.AddHttpClient<IJwtService, JwtService>((sp, httpClient) =>
        {
            KeycloakOptions keycloakOptions = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;

            httpClient.BaseAddress = keycloakOptions.OidcBaseUrl;
        });

        services.AddHttpContextAccessor();

        services.AddScoped<IUserContext, UserContext>();
    }

    private static void AddIdentity(IServiceCollection services)
    {
        // Register the Keycloak client factory, also register the IdentityProvider as a scoped service
        services.AddSingleton<IKeycloakClientFactory, KeycloakClientFactory>();
        services.AddScoped<IIdentityProvider, KeycloakIdentityProvider>();
    }


    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        string connectionString =
            configuration.GetConnectionString("Database") ??
            throw new ArgumentNullException(nameof(configuration));

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<IApartmentRepository, ApartmentRepository>();

        services.AddScoped<IBookingRepository, BookingRepository>();

        services.AddScoped<IReviewRepository, ReviewRepository>();

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddSingleton<ISqlConnectionFactory>(_ =>
            new SqlConnectionFactory(connectionString));

        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
    }

    private static void AddAuthorization(IServiceCollection services)
    {
        services.AddScoped<IAuthorizationService, AuthorizationService>();

        services.AddTransient<IClaimsTransformation, CustomClaimsTransformation>();

        // Register the PermissionAuthorizationHandler, this will be used to handle permission requirements in policies
        services.AddTransient<IAuthorizationHandler, PermissionAuthorizationHandler>();

        // Register the PermissionAuthorizationPolicyProvider, this will be used to create policies based on permissions
        services.AddTransient<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
    }

    private static void AddCaching(IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Cache") ??
                                  throw new ArgumentNullException(nameof(configuration));

        services.AddStackExchangeRedisCache(options => options.Configuration = connectionString);

        services.AddSingleton<ICacheService, CacheService>();
    }

    private static void AddHealthChecks(IServiceCollection services, IConfiguration configuration) =>
        services.AddHealthChecks()
            .AddNpgSql(configuration.GetConnectionString("Database")!)
            .AddRedis(configuration.GetConnectionString("Cache")!)
            .AddUrlGroup(new Uri(configuration["Keycloak:BaseUrl"]!), HttpMethod.Get, "keycloak");

    private static void AddApiVersioning(IServiceCollection services) =>
        // Add API Versioning to the services collection, this is going to allow us to version our API endpoints
        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1);
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc() // needed for ApiVersioning to work with controllers 
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'V"; // e.g., v1, v2
                options.SubstituteApiVersionInUrl = true; // replace the version in the URL
            }); // needed for ApiVersioning to work with Swagger 

    private static void AddBackgroundJobs(IServiceCollection services, IConfiguration configuration)
    {
        // --- Outbox processor job ---
        // Processes domain events that were persisted as outbox messages during SaveChangesAsync.
        services.Configure<OutboxOptions>(configuration.GetSection("Outbox"));

        // --- Complete bookings batch job ---
        // Automatically marks confirmed bookings as completed when their duration end date has passed.
        // Uses raw SQL for performance
        services.Configure<CompleteBookingsJobOptions>(configuration.GetSection("CompleteBookings"));

        // --- Notify completed bookings job ---
        // Sends email notifications to users whose bookings were completed by the batch job.
        // Runs separately because the batch job uses raw SQL and does not generate domain events.
        services.Configure<NotifyCompletedBookingsJobOptions>(configuration.GetSection("NotifyCompletedBookings"));

        services.AddQuartz();

        services.AddQuartzHostedService(options =>
            options.WaitForJobsToComplete =
                true); // Ensure that Quartz jobs are gracefully shutdown when the application stops

        services
            .ConfigureOptions<ProcessOutboxMessagesJobSetup>(); // Configure the Quartz job to process outbox messages
        services.ConfigureOptions<CompleteBookingsJobSetup>(); // Configure the Quartz job to complete bookings
        services
            .ConfigureOptions<
                NotifyCompletedBookingsJobSetup>(); // Configure the Quartz job to notify completed bookings

        AddEmailNotificationResiliencePipeline(services);
    }

    /// <summary>
    /// Registers the Polly resilience pipeline for email notification retries.
    /// Uses exponential backoff (1s, 2s, 4s) with a maximum of 3 retry attempts.
    /// Only retries on transient SMTP and network errors
    /// </summary>
    private static void AddEmailNotificationResiliencePipeline(IServiceCollection services) =>
        services.AddResiliencePipeline("email-notification-retry", builder =>
            builder.AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                // Maximum number of retry attempts before giving up.
                // After 3 failures, the exception propagates to the caller.
                MaxRetryAttempts = 3,

                // Base delay between retries. Combined with exponential backoff,
                // the actual delays will be: 1s, 2s, 4s (doubling each time).
                Delay = TimeSpan.FromSeconds(1),

                // Exponential backoff increases the delay between each retry attempt,
                // giving the external service (SMTP server) more time to recover.
                BackoffType = DelayBackoffType.Exponential,

                ShouldHandle = new PredicateBuilder()
                    .Handle<SmtpCommandException>()
                    .Handle<SmtpProtocolException>()
                    .Handle<IOException>()
                    .Handle<TimeoutException>()
                    .Handle<System.Net.Sockets.SocketException>()
            }));

    private static void AddTurnstile(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TurnstileOptions>(configuration.GetSection("Turnstile"));

        services
            .AddHttpClient<ITurnstileValidator, TurnstileService>((sp, httpClient) =>
            {
                TurnstileOptions options = sp.GetRequiredService<IOptions<TurnstileOptions>>().Value;

                httpClient.BaseAddress = options.BaseUrl;
            })
            .AddResilienceHandler("turnstile-pipeline", AddTurnstileResiliencePipeline);
    }

    /// <summary>
    /// Configures the Polly resilience pipeline for Turnstile HTTP calls.
    /// Combines a Retry strategy for transient errors with a Circuit Breaker
    /// to prevent cascade failures when Cloudflare is experiencing an outage.
    ///
    /// Strategy execution order (outer → inner):
    ///   Circuit Breaker → Retry → HTTP call
    ///
    /// Only handles network/transport errors and HTTP 5xx responses.
    /// Doesnt retry successful responses where success=false (invalid tokens).
    /// </summary>
    private static void AddTurnstileResiliencePipeline(
        ResiliencePipelineBuilder<HttpResponseMessage> builder)
    {
        // --- Circuit Breaker (outer) ---
        // Opens when 50% of requests fail within a 30-second sampling window,
        // requiring at least 5 requests before the circuit can trip.
        // Stays open for 15 seconds before transitioning to half-open.
        builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            // Minimum failure rate (50%) to open the circuit.
            FailureRatio = 0.5,

            // Minimum number of requests in the sampling window before
            // the circuit breaker can evaluate and potentially open.
            MinimumThroughput = 5,

            // Time window used to calculate the failure ratio.
            SamplingDuration = TimeSpan.FromSeconds(30),

            // Time the circuit remains open before attempting recovery (half-open state).
            BreakDuration = TimeSpan.FromSeconds(15),

            // Only trip on network errors and HTTP 5xx — not on valid responses
            // where Cloudflare returns success=false (invalid tokens).
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>()
                .Handle<TimeoutException>()
                .HandleResult(r => (int)r.StatusCode >= 500)
        });

        // --- Retry (inner) ---
        // Fewer attempts: 2
        // Delays with exponential backoff: 1s → 2s.
        builder.AddRetry(new HttpRetryStrategyOptions
        {
            // Maximum number of retry attempts before propagating the exception.
            MaxRetryAttempts = 2,

            // Base delay. With exponential backoff the actual delays will be: 1s, 2s.
            Delay = TimeSpan.FromSeconds(1),

            // Exponential backoff gives Cloudflare more time to recover between retries.
            BackoffType = DelayBackoffType.Exponential,

            // Same predicate as the Circuit Breaker.
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>()
                .Handle<TimeoutException>()
                .HandleResult(r => (int)r.StatusCode >= 500)
        });
    }

    private static void AddOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BookifyAppOptions>(configuration.GetSection("BookifyApp"));
        services.Configure<ExpirationOptions>(configuration.GetSection("Expiration"));
        services.Configure<BookingOptions>(configuration.GetSection("Booking"));
    }

    private static void AddRateLimiting(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(configuration.GetSection("RateLimiting"));

        services.AddSingleton<WriteOperationsRateLimiterPolicy>();
        services.AddSingleton<SearchRateLimiterPolicy>();
        services.AddSingleton<HealthChecksRateLimiterPolicy>();

        RateLimitingOptions rateLimitingOptions = configuration.GetSection("RateLimiting").Get<RateLimitingOptions>() ??
                                                  new RateLimitingOptions();

        services.AddRateLimiter(options =>
        {
            options.AddPolicy<string, WriteOperationsRateLimiterPolicy>("write-operations");
            options.AddPolicy<string, SearchRateLimiterPolicy>("search");
            options.AddPolicy<string, HealthChecksRateLimiterPolicy>("health-checks");

            options.GlobalLimiter =
                System.Threading.RateLimiting.PartitionedRateLimiter
                    .Create<Microsoft.AspNetCore.Http.HttpContext, string>(context =>
                    {
                        string key = context.User.Identity?.IsAuthenticated == true
                            ? context.User.GetIdentityId()
                            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                        return System.Threading.RateLimiting.RateLimitPartition.GetSlidingWindowLimiter(key, _ =>
                            new System.Threading.RateLimiting.SlidingWindowRateLimiterOptions
                            {
                                PermitLimit = rateLimitingOptions.Global.PermitLimit,
                                Window = TimeSpan.FromSeconds(rateLimitingOptions.Global.WindowSeconds),
                                SegmentsPerWindow = rateLimitingOptions.Global.SegmentsPerWindow
                            });
                    });

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode =
                    Microsoft.AspNetCore.Http.StatusCodes.Status429TooManyRequests;
                await Microsoft.AspNetCore.Http.HttpResponseJsonExtensions.WriteAsJsonAsync(
                    context.HttpContext.Response, new Microsoft.AspNetCore.Mvc.ProblemDetails
                    {
                        Status = Microsoft.AspNetCore.Http.StatusCodes.Status429TooManyRequests,
                        Title = "Too Many Requests",
                        Detail = "You have exceeded the request limit. Please try again later."
                    }, token);
            };
        });
    }
}
