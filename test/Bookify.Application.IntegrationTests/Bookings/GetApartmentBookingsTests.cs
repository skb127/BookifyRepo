using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.Bookings.GetBookings;
using Bookify.Application.Common;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Bookings;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Bookings;

public class GetApartmentBookingsTests : BaseIntegrationTest
{
    public GetApartmentBookingsTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetApartmentBookings_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri($"api/v1/apartments/{Guid.NewGuid()}/bookings", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetApartmentBookings_ShouldReturn403_WhenUserIsNotOwnerNorAdmin()
    {
        // Arrange
        var (apartmentId, _, _, _, guestToken, _) = await BookingTestHelpers.SetupApartmentWithOwnerAsync(this);

        // Guest token is used (they are neither admin nor the owner)
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri($"api/v1/apartments/{apartmentId}/bookings", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetApartmentBookings_ShouldReturn404_WhenApartmentNotFound()
    {
        // Arrange
        var (_, _, ownerToken, _, _, _) = await BookingTestHelpers.SetupApartmentWithOwnerAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, ownerToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri($"api/v1/apartments/{Guid.NewGuid()}/bookings", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetApartmentBookings_ShouldReturn400_WhenPageSizeExceeds100()
    {
        // Arrange
        var (apartmentId, _, ownerToken, _, _, _) = await BookingTestHelpers.SetupApartmentWithOwnerAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, ownerToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri($"api/v1/apartments/{apartmentId}/bookings?pageSize=101", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be("Validation error");
    }

    [Fact]
    public async Task GetApartmentBookings_ShouldReturn200_WhenOwnerCallsEndpoint()
    {
        // Arrange
        var (apartmentId, bookingId, ownerToken, _, _, _) = await BookingTestHelpers.SetupApartmentWithOwnerAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, ownerToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri($"api/v1/apartments/{apartmentId}/bookings", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResponse = await response.Content.ReadFromJsonAsync<PagedResponse<BookingSummaryResponse>>();
        pagedResponse.Should().NotBeNull();
        pagedResponse.TotalCount.Should().BeGreaterThanOrEqualTo(1);
        pagedResponse.Items.Should().Contain(b => b.Id == bookingId);
        pagedResponse.Items.Should().AllSatisfy(b => b.ApartmentId.Should().Be(apartmentId));
    }

    [Fact]
    public async Task GetApartmentBookings_ShouldReturn200_WhenAdminCallsEndpoint()
    {
        // Arrange
        var (apartmentId, bookingId, _, _, _, _) = await BookingTestHelpers.SetupApartmentWithOwnerAsync(this);

        // We need an admin token who is NOT the owner to test the admin bypass.
        var adminEmail = $"admin_{Guid.CreateVersion7()}@test.com";
        var ownerEmail = $"owner_{Guid.CreateVersion7()}@test.com";
        var password = "Password123!";

        var registerAdminCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
            adminEmail, "Admin", "User", password, new DateOnly(1990, 1, 1));
        _ = await Sender.Send(registerAdminCommand);
        await PromoteToAdminAsync(adminEmail);

        var registerOwnerCommand = new Bookify.Application.Users.RegisterHost.RegisterHostCommand(
            ownerEmail, "Owner", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        _ = await Sender.Send(registerOwnerCommand);
        
        string adminToken = await GetAccessToken(adminEmail, password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, adminToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri($"api/v1/apartments/{apartmentId}/bookings", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResponse = await response.Content.ReadFromJsonAsync<PagedResponse<BookingSummaryResponse>>();
        pagedResponse.Should().NotBeNull();
        pagedResponse.TotalCount.Should().BeGreaterThanOrEqualTo(1);
        pagedResponse.Items.Should().Contain(b => b.Id == bookingId);
    }

    [Fact]
    public async Task GetApartmentBookings_ShouldReturnEmptyList_WhenApartmentHasNoBookings()
    {
        // Arrange
        // We'll create a fresh apartment with an admin, but no bookings are made for it
        var ownerEmail = $"owner_empty_{Guid.CreateVersion7()}@test.com";
        var password = "Password123!";

        var registerOwnerCommand = new Bookify.Application.Users.RegisterHost.RegisterHostCommand(
            ownerEmail, "AdminOwnerEmpty", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        _ = await Sender.Send(registerOwnerCommand);

        string ownerToken = await GetAccessToken(ownerEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, ownerToken);

        var aptData = Apartments.ApartmentData.ValidCreateApartmentRequest;
        HttpResponseMessage createApartmentResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        createApartmentResponse.EnsureSuccessStatusCode();

        Guid apartmentId = await createApartmentResponse.Content.ReadFromJsonAsync<Guid>();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri($"api/v1/apartments/{apartmentId}/bookings", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResponse = await response.Content.ReadFromJsonAsync<PagedResponse<BookingSummaryResponse>>();
        pagedResponse.Should().NotBeNull();
        pagedResponse.TotalCount.Should().Be(0);
        pagedResponse.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetApartmentBookings_ShouldFilterByStatus_WhenStatusIsProvided()
    {
        // Arrange
        // We'll reuse SetupMultipleBookingsAsync which creates a Reserved and a Confirmed booking.
        var (_, apartmentId, bookingId1Reserved, bookingId2Confirmed, _, _) = await BookingTestHelpers.SetupMultipleBookingsAsync(this);

        var adminEmail = $"admin3_{Guid.CreateVersion7()}@test.com";
        var password = "Password123!";
        var registerAdminCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
            adminEmail, "Admin3", "User", password, new DateOnly(1990, 1, 1));
        _ = await Sender.Send(registerAdminCommand);
        await PromoteToAdminAsync(adminEmail);

        string adminToken = await GetAccessToken(adminEmail, password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, adminToken);

        // Act - request only Confirmed
        int confirmedStatus = (int)BookingStatus.Confirmed;
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri($"api/v1/apartments/{apartmentId}/bookings?status={confirmedStatus}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResponse = await response.Content.ReadFromJsonAsync<PagedResponse<BookingSummaryResponse>>();
        pagedResponse.Should().NotBeNull();

        // Ensure only the confirmed booking returns
        pagedResponse.Items.Should().Contain(b => b.Id == bookingId2Confirmed);
        pagedResponse.Items.Should().NotContain(b => b.Id == bookingId1Reserved);
        pagedResponse.Items.Should().AllSatisfy(b => b.Status.Should().Be(confirmedStatus));
    }
}
