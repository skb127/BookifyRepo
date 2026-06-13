using Bookify.Api.Extensions;
using Bookify.Api.OpenApi;
using Bookify.Application;
using Bookify.Infrastructure;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration)); // Read configuration from appsettings.json

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();

builder.Services.AddProblemDetail();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        foreach (string groupName in app.DescribeApiVersions().Select(x => x.GroupName))
        {
            string url = $"/swagger/{groupName}/swagger.json";
            string name = groupName.ToUpperInvariant();
            options.SwaggerEndpoint(url, name);
        }
    });

    app.ApplyMigrations();

    app.EnsureQuartzSchema();

    app.SeedData(); // Uncomment this line for integration testing to seed data and comment it out for local development to avoid duplicate key errors
}

app.UseHttpsRedirection();

// Use custom middleware to log request context information such as Correlation ID
app.UseRequestContextLogging();

// Enable Serilog request logging, this is going to introduce a middleware that's going to hook into the incoming API requests and start logging useful information about
// the processing of the API requests such as status codes, request times, any exceptions and so on.
app.UseSerilogRequestLogging();

app.UseCustomExceptionHandler();

// Returns the Problem Details response for (empty) non-successful responses
app.UseStatusCodePages();

app.UseAuthentication();

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

// Map Health Checks endpoint
app.MapHealthChecks("health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
}).RequireRateLimiting("health-checks");

await app.RunAsync();

// Make the implicit Program class public so integration tests can access it
#pragma warning disable CA1515
public partial class Program
{
    protected Program()
    {
    }
}
#pragma warning restore CA1515
