using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Application.IntegrationTests.Users;

public class UserStripeLifecycleTests : BaseIntegrationTest
{
    private readonly MockStripeCustomerService _mockStripeCustomerService;
    private readonly MockEmailService _mockEmailService;

    public UserStripeLifecycleTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _mockStripeCustomerService = factory.MockStripeCustomerService;
        _mockEmailService = factory.MockEmailService;
    }

    [Fact]
    public async Task UserStripeLifecycle_Register_ShouldCreateStripeCustomer_WhenUserRegistersSuccessfully()
    {
        // Arrange
        var email = $"stripe-lifecycle-reg-{Guid.NewGuid()}@test.com";
        var firstName = "Stripe";
        var lastName = "Customer";
        var password = "ClaveSegura1$";
        var request = new RegisterUserRequest(email, firstName, lastName, password, new DateOnly(2000, 1, 1));

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/users/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Poll with timeout for Outbox and background job to process the UserCreatedDomainEvent
        var timeoutAt = DateTime.UtcNow.AddSeconds(15);
        bool callReceived = false;

        while (DateTime.UtcNow < timeoutAt)
        {
            if (_mockStripeCustomerService.UpsertCalls.TryPeek(out var call) && call.Email == email)
            {
                callReceived = true;
                break;
            }
            await Task.Delay(200);
        }

        callReceived.Should().BeTrue("Stripe customer upsert should have been called via Outbox");

        // Verify the database has the stripe_customer_id set
        var dbUser = await DbContext.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == new Email(email));

        dbUser.Should().NotBeNull();
        dbUser.StripeCustomerId.Should().NotBeNullOrWhiteSpace();
        dbUser.StripeCustomerId.Should().StartWith("cus_mock_");
    }

    [Fact]
    public async Task UserStripeLifecycle_UpdateProfile_ShouldUpdateStripeCustomer_WhenProfileIsUpdated()
    {
        // Arrange - Register a new user first
        var email = $"stripe-lifecycle-upd-{Guid.NewGuid()}@test.com";
        var firstName = "InitialFirst";
        var lastName = "InitialLast";
        var password = "ClaveSegura1$";
        var registerRequest = new RegisterUserRequest(email, firstName, lastName, password, new DateOnly(2000, 1, 1));

        HttpResponseMessage registerResponse = await HttpClient.PostAsJsonAsync("api/v1/users/register", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Wait for upsert to finish so StripeCustomerId is set
        var timeoutAt = DateTime.UtcNow.AddSeconds(15);
        string? stripeCustomerId = null;
        while (DateTime.UtcNow < timeoutAt)
        {
            var dbUser = await DbContext.Set<User>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == new Email(email));
            if (dbUser is not null && !string.IsNullOrWhiteSpace(dbUser.StripeCustomerId))
            {
                stripeCustomerId = dbUser.StripeCustomerId;
                break;
            }
            await Task.Delay(200);
        }

        stripeCustomerId.Should().NotBeNullOrWhiteSpace("StripeCustomerId should be generated and saved");

        // Authenticate as this user
        string accessToken = await GetAccessToken(email, password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        var updatedFirstName = "UpdatedFirst";
        var updatedLastName = "UpdatedLast";
        var updateRequest = new UpdateUserProfileRequest(
            updatedFirstName,
            updatedLastName,
            "+34612345678",
            new DateOnly(2000, 1, 1),
            password);

        // Act
        HttpResponseMessage updateResponse = await HttpClient.PutAsJsonAsync("api/v1/users/profile", updateRequest);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Poll with timeout for UpdateCustomerAsync call
        timeoutAt = DateTime.UtcNow.AddSeconds(15);
        bool updateCallReceived = false;
        while (DateTime.UtcNow < timeoutAt)
        {
            if (_mockStripeCustomerService.UpdateCalls.TryPeek(out var call) && call.CustomerId == stripeCustomerId)
            {
                call.Name.Should().Be($"{updatedFirstName} {updatedLastName}");
                call.Email.Should().Be(email);
                updateCallReceived = true;
                break;
            }
            await Task.Delay(200);
        }

        updateCallReceived.Should().BeTrue("Stripe customer update should have been called via Outbox");
    }

    [Fact]
    public async Task UserStripeLifecycle_ConfirmEmailChange_ShouldUpdateStripeCustomer_WhenEmailIsChanged()
    {
        // Arrange - Register user first
        var email = $"stripe-lifecycle-email-{Guid.NewGuid()}@test.com";
        var firstName = "Email";
        var lastName = "Changer";
        var password = "ClaveSegura1$";
        var registerRequest = new RegisterUserRequest(email, firstName, lastName, password, new DateOnly(2000, 1, 1));

        HttpResponseMessage registerResponse = await HttpClient.PostAsJsonAsync("api/v1/users/register", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Wait for StripeCustomerId
        var timeoutAt = DateTime.UtcNow.AddSeconds(15);
        string? stripeCustomerId = null;
        while (DateTime.UtcNow < timeoutAt)
        {
            var dbUser = await DbContext.Set<User>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == new Email(email));
            if (dbUser is not null && !string.IsNullOrWhiteSpace(dbUser.StripeCustomerId))
            {
                stripeCustomerId = dbUser.StripeCustomerId;
                break;
            }
            await Task.Delay(200);
        }

        stripeCustomerId.Should().NotBeNullOrWhiteSpace();

        // Login as the user
        string accessToken = await GetAccessToken(email, password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, accessToken);

        var newEmail = $"stripe-lifecycle-newemail-{Guid.NewGuid()}@test.com";
        DateTime since = DateTime.UtcNow;

        // Act 1 - Initiate email change
        var initiateRequest = new InitiateEmailChangeRequest(newEmail, password);
        HttpResponseMessage initiateResponse = await HttpClient.PostAsJsonAsync("api/v1/users/change-email", initiateRequest);
        initiateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Wait for verification email
        EmailMessage verificationEmail = await _mockEmailService.WaitForEmailToAsync(newEmail, since: since);
        string token = EmailTestUtils.ExtractToken(verificationEmail.Body, "confirm-email-change");
        token.Should().NotBeNullOrEmpty();

        // Act 2 - Confirm email change (anonymous endpoint)
        HttpClient.DefaultRequestHeaders.Authorization = null;
        var confirmRequest = new ConfirmEmailChangeRequest(token);
        HttpResponseMessage confirmResponse = await HttpClient.PostAsJsonAsync("api/v1/users/confirm-email-change", confirmRequest);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Poll with timeout for UpdateCustomerAsync call
        timeoutAt = DateTime.UtcNow.AddSeconds(15);
        bool emailCallReceived = false;
        while (DateTime.UtcNow < timeoutAt)
        {
            if (_mockStripeCustomerService.UpdateCalls.TryPeek(out var call) && 
                call.CustomerId == stripeCustomerId && 
                call.Email == newEmail)
            {
                call.Name.Should().Be($"{firstName} {lastName}");
                emailCallReceived = true;
                break;
            }
            await Task.Delay(200);
        }

        emailCallReceived.Should().BeTrue("Stripe customer update should have been called with new email via Outbox");
    }

    [Fact(Skip = "User deletion not implemented yet — will be enabled in the delete-user feature phase")]
    public Task UserStripeLifecycle_DeleteUser_ShouldDeactivateStripeCustomer_WhenUserIsDeleted()
    {
        // Act (Placeholder for delete action)
        return Task.CompletedTask;
    }
}
