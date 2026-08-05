using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Bookify.Api.Controllers.Users.Requests;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.IntegrationTests.Bookings;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.Users.GetUserById;
using Bookify.Application.Users.RegisterHost;
using Bookify.Domain.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Application.IntegrationTests.Users;

public class AccountDeactivationTests : BaseIntegrationTest
{
    private readonly MockEmailService _mockEmailService;

    public AccountDeactivationTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _mockEmailService = factory.MockEmailService;
    }

    private async Task WaitForUserOutboxProcessingAsync(string email)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < timeoutAt)
        {
            DbContext.ChangeTracker.Clear();
            User? u = await DbContext.Set<User>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Email == new Email(email))
                .ConfigureAwait(false);

            if (u?.StripeCustomerId is not null)
            {
                await Task.Delay(200).ConfigureAwait(false);
                return;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task Deactivate_ShouldReturnSuccess_AndSetPendingDeletion()
    {
        // Arrange: register user via HTTP API
        string email = $"deact_{Guid.CreateVersion7()}@test.com";
        string password = "Password123!";
        var registerRequest = new RegisterUserRequest(email, "Deact", "User", password, new DateOnly(1995, 1, 1));
        HttpResponseMessage regResponse =
            await HttpClient.PostAsJsonAsync("api/v1/users/register/guest", registerRequest);
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await WaitForUserOutboxProcessingAsync(email);

        string accessToken = await GetAccessToken(email, password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act: POST /api/v1/users/deactivate
        HttpResponseMessage response = await HttpClient.PostAsync(
            new Uri("api/v1/users/deactivate", UriKind.Relative),
            null);

        // Assert
        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        // Verify status via HTTP API (using Admin token to query user endpoint)
        string adminEmail = UserData.GetUserByIdUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);
        string adminToken = await GetAccessToken(adminEmail, UserData.GetUserByIdUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            adminToken);

        User dbUser = await DbContext.Set<User>().AsNoTracking().FirstAsync(u => u.Email == new Email(email));
        HttpResponseMessage getResponse =
            await HttpClient.GetAsync(new Uri($"api/v1/users/{dbUser.Id}", UriKind.Relative));
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var userResponse = await getResponse.Content.ReadFromJsonAsync<AdminUserResponse>();
        userResponse.Should().NotBeNull();
        userResponse.StatusName.Should().Be("PendingDeletion");
        userResponse.DeletionScheduledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelDeactivation_ShouldRestoreActiveStatus()
    {
        // Arrange: register user via HTTP API
        string email = $"cancel_{Guid.CreateVersion7()}@test.com";
        string password = "Password123!";
        var registerRequest = new RegisterUserRequest(email, "Cancel", "User", password, new DateOnly(1995, 1, 1));
        HttpResponseMessage regResponse =
            await HttpClient.PostAsJsonAsync("api/v1/users/register/guest", registerRequest);
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await WaitForUserOutboxProcessingAsync(email);

        string accessToken = await GetAccessToken(email, password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        HttpResponseMessage deactResponse = await HttpClient.PostAsync(
            new Uri("api/v1/users/deactivate", UriKind.Relative),
            null);
        string deactBody = await deactResponse.Content.ReadAsStringAsync();
        deactResponse.StatusCode.Should().Be(HttpStatusCode.OK, deactBody);

        // Wait for deactivation email containing the token
        EmailMessage emailMsg = await _mockEmailService.WaitForEmailToAsync(email, "Account Deactivation Request");
        Match match = Regex.Match(emailMsg.Body, "token=([A-Za-z0-9_-]+)");
        match.Success.Should().BeTrue();
        string rawToken = match.Groups[1].Value;

        // Act: POST /api/v1/users/cancel-deactivation
        var cancelRequest = new CancelAccountDeletionRequest(rawToken);
        HttpResponseMessage cancelResponse = await HttpClient.PostAsJsonAsync(
            "api/v1/users/cancel-deactivation",
            cancelRequest);

        // Assert
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify that cancellation email was sent via Outbox
        EmailMessage cancelEmail = await _mockEmailService.WaitForEmailToAsync(email, "Account Deactivation Cancelled");
        cancelEmail.Should().NotBeNull();

        // Verify status via HTTP API
        string adminEmail = UserData.GetUserByIdUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);
        string adminToken = await GetAccessToken(adminEmail, UserData.GetUserByIdUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            adminToken);

        User dbUser = await DbContext.Set<User>().AsNoTracking().FirstAsync(u => u.Email == new Email(email));
        HttpResponseMessage getResponse =
            await HttpClient.GetAsync(new Uri($"api/v1/users/{dbUser.Id}", UriKind.Relative));
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var userResponse = await getResponse.Content.ReadFromJsonAsync<AdminUserResponse>();
        userResponse.Should().NotBeNull();
        userResponse.StatusName.Should().Be("Active");
        userResponse.DeletionScheduledAt.Should().BeNull();
    }

    [Fact]
    public async Task Deactivate_ShouldReturn400_WhenGuestHasActiveBookings()
    {
        // Arrange: Setup apartment and guest reserved booking
        var (_, _, _, guestToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            guestToken);

        // Act: Guest attempts deactivation while having an active booking
        HttpResponseMessage response = await HttpClient.PostAsync(
            new Uri("api/v1/users/deactivate", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Detail.Should().Be(UserErrors.HasActiveBookingsAsGuest.Name);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn400_WhenHostHasActiveBookings()
    {
        // Arrange: Setup apartment with host and reserved booking
        var (_, _, hostToken, _, _, _) = await BookingTestHelpers.SetupApartmentWithOwnerAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            hostToken);

        // Act: Host attempts deactivation while apartment has an active booking
        HttpResponseMessage response = await HttpClient.PostAsync(
            new Uri("api/v1/users/deactivate", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Detail.Should().Be(UserErrors.HasActiveBookingsAsHost.Name);
    }

    [Fact]
    public async Task Deactivate_ShouldSucceed_WhenUserIsHostWithNoActiveBookings()
    {
        // Arrange: Register a Host user with no active bookings
        string hostEmail = $"host_no_bookings_{Guid.CreateVersion7()}@test.com";
        string password = "Password123!";
        var registerHostCommand = new RegisterHostCommand(hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1),
            "+34612345678");
        Guid hostId = (await Sender.Send(registerHostCommand)).Value;

        await WaitForUserOutboxProcessingAsync(hostEmail);

        string hostToken = await GetAccessToken(hostEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            hostToken);

        // Act: Host requests account deactivation
        HttpResponseMessage response = await HttpClient.PostAsync(
            new Uri("api/v1/users/deactivate", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify status via HTTP API
        string adminEmail = UserData.GetUserByIdUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);
        string adminToken = await GetAccessToken(adminEmail, UserData.GetUserByIdUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            adminToken);

        HttpResponseMessage getResponse =
            await HttpClient.GetAsync(new Uri($"api/v1/users/{hostId}", UriKind.Relative));
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var userResponse = await getResponse.Content.ReadFromJsonAsync<AdminUserResponse>();
        userResponse.Should().NotBeNull();
        userResponse.StatusName.Should().Be("PendingDeletion");
        userResponse.DeletionScheduledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Deactivate_ShouldReturn400_WhenUserIsAlreadyPendingDeletion()
    {
        // Arrange
        string email = $"double_deact_{Guid.CreateVersion7()}@test.com";
        string password = "Password123!";
        var registerRequest = new RegisterUserRequest(email, "Double", "Deact", password, new DateOnly(1995, 1, 1));
        HttpResponseMessage regResponse =
            await HttpClient.PostAsJsonAsync("api/v1/users/register/guest", registerRequest);
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await WaitForUserOutboxProcessingAsync(email);

        string accessToken = await GetAccessToken(email, password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // First deactivation succeeds
        HttpResponseMessage deactResponse = await HttpClient.PostAsync(
            new Uri("api/v1/users/deactivate", UriKind.Relative),
            null);
        deactResponse.EnsureSuccessStatusCode();

        // Act: Try deactivating again
        HttpResponseMessage secondDeactResponse = await HttpClient.PostAsync(
            new Uri("api/v1/users/deactivate", UriKind.Relative),
            null);

        // Assert
        secondDeactResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails =
            await secondDeactResponse.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Detail.Should().Be(UserErrors.RequestFailed.Name);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn401_WhenUnauthenticated()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync(
            new Uri("api/v1/users/deactivate", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CancelDeactivation_ShouldReturn400_WhenTokenIsInvalidOrExpired()
    {
        // Act: Try canceling with invalid token
        var cancelRequest = new CancelAccountDeletionRequest("invalid_token_123");
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "api/v1/users/cancel-deactivation",
            cancelRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Detail.Should().Be(UserErrors.InvalidToken.Name);
    }

    [Fact]
    public async Task FinalizeAccountDeletion_ShouldSoftDeleteUser_WhenGracePeriodExpires()
    {
        // Arrange: Register user and request deactivation
        string email = $"finalize_{Guid.CreateVersion7()}@test.com";
        string password = "Password123!";
        var registerRequest = new RegisterUserRequest(email, "Finalize", "User", password, new DateOnly(1995, 1, 1));
        HttpResponseMessage regResponse =
            await HttpClient.PostAsJsonAsync("api/v1/users/register/guest", registerRequest);
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await WaitForUserOutboxProcessingAsync(email);

        string accessToken = await GetAccessToken(email, password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act: User requests account deactivation
        HttpResponseMessage deactResponse = await HttpClient.PostAsync(
            new Uri("api/v1/users/deactivate", UriKind.Relative),
            null);
        deactResponse.EnsureSuccessStatusCode();

        // Verify that account deactivation request email was sent via Outbox
        EmailMessage reqEmail = await _mockEmailService.WaitForEmailToAsync(email, "Account Deactivation Request");
        reqEmail.Should().NotBeNull();

        User dbUser = await DbContext.Set<User>().AsNoTracking().FirstAsync(u => u.Email == new Email(email));

        // Setup Admin token for HTTP API status verification
        string adminEmail = UserData.GetUserByIdUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);
        string adminToken = await GetAccessToken(adminEmail, UserData.GetUserByIdUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            adminToken);

        // Assert: Poll HTTP API for status transition to "Deleted" performed automatically by FinalizeAccountDeletionJob
        var timeoutAt = DateTime.UtcNow.AddSeconds(15);
        bool isDeleted = false;
        AdminUserResponse? userResponse = null;

        while (DateTime.UtcNow < timeoutAt)
        {
            HttpResponseMessage getResponse =
                await HttpClient.GetAsync(new Uri($"api/v1/users/{dbUser.Id}", UriKind.Relative));
            if (getResponse.IsSuccessStatusCode)
            {
                userResponse = await getResponse.Content.ReadFromJsonAsync<AdminUserResponse>();
                if (userResponse?.StatusName == "Deleted")
                {
                    isDeleted = true;
                    break;
                }
            }

            await Task.Delay(500);
        }

        isDeleted.Should()
            .BeTrue("the background FinalizeAccountDeletionJob should run and soft-delete the user after grace period");
        userResponse.Should().NotBeNull();
        userResponse.StatusName.Should().Be("Deleted");
        userResponse.DeletedAt.Should().NotBeNull();
    }
}
