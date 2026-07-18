using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.TaxRules;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.TaxRules;
using Bookify.Domain.TaxRules;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.TaxRules;

public class TaxRulesTests : BaseIntegrationTest
{
    private const string Password = "Password123!";

    public TaxRulesTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CreateTaxRule_ShouldReturn201Created_WhenAdminUser()
    {
        // Arrange
        string adminToken = await SetupAdminTokenAsync();
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, adminToken);

        var request = new CreateTaxRuleRequest(
            "ES",
            "Madrid",
            "Madrid",
            0.21m,
            1, // Percentage
            "VAT ES",
            new DateOnly(2026, 1, 1),
            null)
        {
            RateValue = 0.21m,
            RateType = 1,
            EffectiveFrom = new DateOnly(2026, 1, 1)
        };

        // Act
        HttpResponseMessage response =
            await HttpClient.PostAsJsonAsync(new Uri("api/v1/tax-rules", UriKind.Relative), request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var taxRuleId = await response.Content.ReadFromJsonAsync<Guid>();
        taxRuleId.Should().NotBeEmpty();

        var getResponse = await HttpClient.GetAsync(new Uri($"api/v1/tax-rules/{taxRuleId}", UriKind.Relative));
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var taxRule = await getResponse.Content.ReadFromJsonAsync<TaxRuleResponse>();
        taxRule.Should().NotBeNull();
        taxRule.Name.Should().Be("VAT ES");
        taxRule.CountryCode.Should().Be("ES");
    }

    [Fact]
    public async Task CreateTaxRule_ShouldReturn403Forbidden_WhenNotAdmin()
    {
        // Arrange
        string guestToken = await SetupGuestTokenAsync();
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var request = new CreateTaxRuleRequest(
            "ES",
            "Madrid",
            "Madrid",
            0.21m,
            1, // Percentage
            "VAT ES",
            new DateOnly(2026, 1, 1),
            null)
        {
            RateValue = 0.21m,
            RateType = 1,
            EffectiveFrom = new DateOnly(2026, 1, 1)
        };

        // Act
        HttpResponseMessage response =
            await HttpClient.PostAsJsonAsync(new Uri("api/v1/tax-rules", UriKind.Relative), request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateTaxRule_ShouldReturn204NoContent_WhenAdminUser()
    {
        // Arrange
        string adminToken = await SetupAdminTokenAsync();
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, adminToken);

        var createRequest = new CreateTaxRuleRequest(
            "ES",
            "Madrid",
            "Madrid",
            0.21m,
            1,
            "VAT ES",
            new DateOnly(2026, 1, 1),
            null)
        {
            RateValue = 0.21m,
            RateType = 1,
            EffectiveFrom = new DateOnly(2026, 1, 1)
        };

        HttpResponseMessage createResponse =
            await HttpClient.PostAsJsonAsync(new Uri("api/v1/tax-rules", UriKind.Relative), createRequest);
        var taxRuleId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        var updateRequest = new UpdateTaxRuleRequest(
            "ES",
            "Madrid",
            "Madrid",
            0.10m, // Change rate to 10%
            1,
            "VAT ES Reduced",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31))
        {
            RateValue = 0.10m,
            RateType = 1,
            EffectiveFrom = new DateOnly(2026, 1, 1)
        };

        // Act
        HttpResponseMessage response =
            await HttpClient.PutAsJsonAsync(new Uri($"api/v1/tax-rules/{taxRuleId}", UriKind.Relative), updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await HttpClient.GetAsync(new Uri($"api/v1/tax-rules/{taxRuleId}", UriKind.Relative));
        var taxRule = await getResponse.Content.ReadFromJsonAsync<TaxRuleResponse>();
        taxRule.Should().NotBeNull();
        taxRule.Name.Should().Be("VAT ES Reduced");
        taxRule.RateValue.Should().Be(0.10m);
    }

    [Fact]
    public async Task DeactivateTaxRule_ShouldReturn204NoContent_WhenAdminUser()
    {
        // Arrange
        string adminToken = await SetupAdminTokenAsync();
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, adminToken);

        var createRequest = new CreateTaxRuleRequest(
            "ES",
            "Madrid",
            "Madrid",
            0.21m,
            1,
            "VAT ES",
            new DateOnly(2026, 1, 1),
            null)
        {
            RateValue = 0.21m,
            RateType = 1,
            EffectiveFrom = new DateOnly(2026, 1, 1)
        };

        HttpResponseMessage createResponse =
            await HttpClient.PostAsJsonAsync(new Uri("api/v1/tax-rules", UriKind.Relative), createRequest);
        var taxRuleId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        // Act
        HttpResponseMessage response =
            await HttpClient.DeleteAsync(new Uri($"api/v1/tax-rules/{taxRuleId}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await HttpClient.GetAsync(new Uri($"api/v1/tax-rules/{taxRuleId}", UriKind.Relative));
        var taxRule = await getResponse.Content.ReadFromJsonAsync<TaxRuleResponse>();
        taxRule.Should().NotBeNull();
        taxRule.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetTaxRules_ShouldReturn401Unauthorized_WhenNotAuthenticated()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/tax-rules", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateTaxRule_ShouldReturn403Forbidden_WhenNotAdmin()
    {
        // Arrange
        string guestToken = await SetupGuestTokenAsync();
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var updateRequest = new UpdateTaxRuleRequest(
            "ES",
            "Madrid",
            "Madrid",
            0.10m,
            1,
            "VAT ES Reduced",
            new DateOnly(2026, 1, 1),
            null)
        {
            RateValue = 0.10m,
            RateType = 1,
            EffectiveFrom = new DateOnly(2026, 1, 1)
        };

        // Act
        HttpResponseMessage response =
            await HttpClient.PutAsJsonAsync(new Uri($"api/v1/tax-rules/{Guid.NewGuid()}", UriKind.Relative),
                updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeactivateTaxRule_ShouldReturn403Forbidden_WhenNotAdmin()
    {
        // Arrange
        string guestToken = await SetupGuestTokenAsync();
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act
        HttpResponseMessage response =
            await HttpClient.DeleteAsync(new Uri($"api/v1/tax-rules/{Guid.NewGuid()}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTaxRule_ShouldReturn404NotFound_WhenTaxRuleDoesNotExist()
    {
        // Arrange
        string guestToken = await SetupGuestTokenAsync();
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act
        HttpResponseMessage response =
            await HttpClient.GetAsync(new Uri($"api/v1/tax-rules/{Guid.NewGuid()}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateTaxRule_ShouldReturn404NotFound_WhenTaxRuleDoesNotExist()
    {
        // Arrange
        string adminToken = await SetupAdminTokenAsync();
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, adminToken);

        var updateRequest = new UpdateTaxRuleRequest(
            "ES",
            "Madrid",
            "Madrid",
            0.10m,
            1,
            "VAT ES Reduced",
            new DateOnly(2026, 1, 1),
            null)
        {
            RateValue = 0.10m,
            RateType = 1,
            EffectiveFrom = new DateOnly(2026, 1, 1)
        };

        // Act
        HttpResponseMessage response =
            await HttpClient.PutAsJsonAsync(new Uri($"api/v1/tax-rules/{Guid.NewGuid()}", UriKind.Relative),
                updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeactivateTaxRule_ShouldReturn404NotFound_WhenTaxRuleDoesNotExist()
    {
        // Arrange
        string adminToken = await SetupAdminTokenAsync();
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, adminToken);

        // Act
        HttpResponseMessage response =
            await HttpClient.DeleteAsync(new Uri($"api/v1/tax-rules/{Guid.NewGuid()}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be(TaxRuleErrors.NotFound.Code);
    }

    [Fact]
    public async Task DeactivateTaxRule_ShouldReturn400BadRequest_WhenTaxRuleIsAlreadyInactive()
    {
        // Arrange
        string adminToken = await SetupAdminTokenAsync();
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, adminToken);

        var createRequest = new CreateTaxRuleRequest(
            "ES",
            "Madrid",
            "Madrid",
            0.21m,
            1,
            "VAT ES",
            new DateOnly(2026, 1, 1),
            null)
        {
            RateValue = 0.21m,
            RateType = 1,
            EffectiveFrom = new DateOnly(2026, 1, 1)
        };

        HttpResponseMessage createResponse =
            await HttpClient.PostAsJsonAsync(new Uri("api/v1/tax-rules", UriKind.Relative), createRequest);
        var taxRuleId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        // Deactivate once
        HttpResponseMessage response1 =
            await HttpClient.DeleteAsync(new Uri($"api/v1/tax-rules/{taxRuleId}", UriKind.Relative));
        response1.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act - Deactivate again
        HttpResponseMessage response2 =
            await HttpClient.DeleteAsync(new Uri($"api/v1/tax-rules/{taxRuleId}", UriKind.Relative));

        // Assert
        response2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response2.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be(TaxRuleErrors.AlreadyInactive.Code);
    }

    private async Task<string> SetupAdminTokenAsync()
    {
        var adminEmail = $"admin_{Guid.NewGuid()}@test.com";
        var registerAdminCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            adminEmail, "Admin", "User", Password, new DateOnly(1990, 1, 1));
        await Sender.Send(registerAdminCommand).ConfigureAwait(false);
        await PromoteToAdminAsync(adminEmail).ConfigureAwait(false);
        return await GetAccessToken(adminEmail, Password).ConfigureAwait(false);
    }

    private async Task<string> SetupGuestTokenAsync()
    {
        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            guestEmail, "Guest", "User", Password, new DateOnly(1995, 5, 5));
        await Sender.Send(registerGuestCommand).ConfigureAwait(false);
        return await GetAccessToken(guestEmail, Password).ConfigureAwait(false);
    }
}
