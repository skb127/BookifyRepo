using System.Net.Http.Json;
using Bookify.Api.Controllers.Users;
using Bookify.Application.Abstractions.Caching;
using Bookify.Application.Users;
using Bookify.Domain.Users;
using Bookify.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Application.IntegrationTests.Infrastructure;

[Collection("IntegrationTests")]
public abstract class BaseIntegrationTest
{
    private readonly IServiceScope _scope; // To allow resolving scoped services
    public ISender Sender { get; } // To send commands/queries via MediatR
    public ApplicationDbContext DbContext { get; }  // To interact with the database
    public HttpClient HttpClient { get; } // To make HTTP requests to the test server

    protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        factory.MockEmailService.Clear();
        factory.MockPaymentGateway.Clear();

        _scope = factory.Services.CreateScope();

        Sender = _scope.ServiceProvider.GetRequiredService<ISender>();
        DbContext = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        HttpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        HttpClient.DefaultRequestHeaders.Add("X-Turnstile-Token", "XXXX.DUMMY.TOKEN.XXXX");
    }

    public async Task<string> GetAccessToken(string userEmail, string userPassword)
    {
        HttpResponseMessage loginResponse = await HttpClient.PostAsJsonAsync(
            "api/v1/users/login",
            new LoginUserRequest(
                userEmail,
                userPassword)).ConfigureAwait(false);

        AccessTokenOnlyResponse? accessTokenResponse = await loginResponse.Content.ReadFromJsonAsync<AccessTokenOnlyResponse>().ConfigureAwait(false);

        return accessTokenResponse?.AccessToken ?? throw new InvalidOperationException("Unable to get access token");
    }

    public async Task PromoteToAdminAsync(string email)
    {
        var cacheService = _scope.ServiceProvider.GetRequiredService<ICacheService>();

        User user = await DbContext.Set<User>()
            .AsNoTracking()
            .FirstAsync(u => u.Email == new Email(email))
            .ConfigureAwait(false);

        // Check if the user already has the Admin role to avoid duplicate key errors
        bool alreadyAdmin = await DbContext.Database
            .SqlQuery<int>($"SELECT COUNT(1) AS \"Value\" FROM role_user WHERE roles_id = {Role.Admin.Id} AND users_id = {user.Id}")
            .AnyAsync(c => c > 0)
            .ConfigureAwait(false);

        if (!alreadyAdmin)
        {
            await DbContext.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO role_user (roles_id, users_id) VALUES ({Role.Admin.Id}, {user.Id})")
                .ConfigureAwait(false);
        }

        // Invalidate cached roles/permissions so the authorization pipeline re-reads from DB
        string identityId = user.IdentityId;
        await cacheService.RemoveAsync($"auth:roles-{identityId}").ConfigureAwait(false);
        await cacheService.RemoveAsync($"auth:permissions-{identityId}").ConfigureAwait(false);
    }

    protected async Task<string> GetAdminTokenAsync(string password = "Password123!")
    {
        var adminEmail = $"admin_{Guid.CreateVersion7()}@test.com";
        var registerAdminCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            adminEmail, "Admin", "User", password, new DateOnly(1990, 1, 1));
        _ = await Sender.Send(registerAdminCommand).ConfigureAwait(false);
        await PromoteToAdminAsync(adminEmail).ConfigureAwait(false);
        return await GetAccessToken(adminEmail, password).ConfigureAwait(false);
    }
}
