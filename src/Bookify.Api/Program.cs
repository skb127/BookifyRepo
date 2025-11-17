using Bookify.Api.Extensions;
using Bookify.Application;
using Bookify.Infrastructure;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration)); // Read configuration from appsettings.json

builder.Services.AddControllers();
//builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    //app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();

    app.ApplyMigrations();

    //app.SeedData();
}

app.UseHttpsRedirection();

// Use custom middleware to log request context information such as Correlation ID
app.UseRequestContextLogging();

// Enable Serilog request logging, this is going to introduce a middleware that's going to hook into the incoming API requests and start logging useful information about
// the processing of the API requests such as status codes, request times, any exceptions and so on.
app.UseSerilogRequestLogging();

app.UseCustomExceptionHandler();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
