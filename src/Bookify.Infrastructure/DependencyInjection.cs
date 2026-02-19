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
using Bookify.Infrastructure.Caching;
using Bookify.Infrastructure.Clock;
using Bookify.Infrastructure.Data;
using Bookify.Infrastructure.Email;
using Bookify.Infrastructure.Identity;
using Bookify.Infrastructure.Outbox;
using Bookify.Infrastructure.Repositories;
using Bookify.Infrastructure.Security;
using Dapper;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;
using AuthenticationOptions = Bookify.Infrastructure.Authentication.AuthenticationOptions;
using AuthenticationService = Bookify.Infrastructure.Authentication.AuthenticationService;
using IAuthenticationService = Bookify.Application.Abstractions.Authentication.IAuthenticationService;

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

        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

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
        services.AddScoped<AuthorizationService>();

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
        services.Configure<OutboxOptions>(configuration.GetSection("Outbox"));

        services.AddQuartz();

        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true); // Ensure that Quartz jobs are gracefully shutdown when the application stops

        services.ConfigureOptions<ProcessOutboxMessagesJobSetup>(); // Configure the Quartz job to process outbox messages, this is going to be triggered based on the schedule defined in the OutboxOptions
    }

    private static void AddTurnstile(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TurnstileOptions>(configuration.GetSection("Turnstile"));

        services.AddHttpClient<ITurnstileValidator, TurnstileService>((sp, httpClient) =>
        {
            TurnstileOptions options = sp.GetRequiredService<IOptions<TurnstileOptions>>().Value;
            
            httpClient.BaseAddress = options.BaseUrl;
        });
    }

    private static void AddOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BookifyAppOptions>(configuration.GetSection("BookifyApp"));
        services.Configure<ExpirationOptions>(configuration.GetSection("Expiration"));
    }
}
