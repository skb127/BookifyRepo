using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Bookings;
using Bookify.Application.Bookings.GetBooking;
using Bookify.Application.IntegrationTests.Apartments;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Bookings;

public class ReserveBookingTests : BaseIntegrationTest
{
    public ReserveBookingTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn201_WhenRequestIsValid()
    {
        // Arrange
        // 1. Create an admin to create the apartment
        var adminEmail = $"admin_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerAdminCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            adminEmail, "Admin", "User", password, new DateOnly(1990, 1, 1));
        await Sender.Send(registerAdminCommand);
        await PromoteToAdminAsync(adminEmail);

        // 2. Create apartment mapping to Admin context
        string adminToken = await GetAccessToken(adminEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, adminToken);
        var aptData = ApartmentData.ValidCreateApartmentRequest;
        HttpResponseMessage aptResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>();

        // 3. Create a guest to reserve
        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        var guestUserIdResponse = await Sender.Send(registerGuestCommand);
        var guestUserId = guestUserIdResponse.Value;

        string guestToken = await GetAccessToken(guestEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var request = new ReserveBookingRequest(
            apartmentId,
            new DateOnly(2028, 1, 1),
            new DateOnly(2028, 1, 10))
        {
            ApartmentId = apartmentId,
            StartDate = new DateOnly(2028, 1, 1),
            EndDate = new DateOnly(2028, 1, 10)
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/bookings", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().StartWith($"https://localhost/api/v1/bookings/");

        var bookingId = await response.Content.ReadFromJsonAsync<Guid>();
        bookingId.Should().NotBeEmpty();

        // Verify the booking was created in DB for the GUEST
        HttpResponseMessage getResponse = await HttpClient.GetAsync(response.Headers.Location);
        getResponse.EnsureSuccessStatusCode();
        var bookingResponse = await getResponse.Content.ReadFromJsonAsync<BookingResponse>();
        bookingResponse.Should().NotBeNull();
        bookingResponse.UserId.Should().Be(guestUserId);
        bookingResponse.Status.Should().Be((int)Domain.Bookings.BookingStatus.PendingPayment);
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        var request = new ReserveBookingRequest(
            Guid.NewGuid(),
            new DateOnly(2028, 1, 1),
            new DateOnly(2028, 1, 10))
        {
            ApartmentId = Guid.NewGuid(),
            StartDate = new DateOnly(2028, 1, 1),
            EndDate = new DateOnly(2028, 1, 10)
        };

        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/bookings", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn201AndStatusPendingPayment_WhenApartmentHasInstantBooking()
    {
        // Arrange
        // 1. Create an admin to create the apartment
        var adminEmail = $"admin_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerAdminCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            adminEmail, "Admin", "User", password, new DateOnly(1990, 1, 1));
        await Sender.Send(registerAdminCommand);
        await PromoteToAdminAsync(adminEmail);

        // 2. Create apartment mapping to Admin context with InstantBooking = true
        string adminToken = await GetAccessToken(adminEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, adminToken);
        var aptData = ApartmentData.ValidCreateApartmentInstantBookingRequest;
        HttpResponseMessage aptResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>();

        // 3. Create a guest to reserve
        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        var guestUserIdResponse = await Sender.Send(registerGuestCommand);
        var guestUserId = guestUserIdResponse.Value;

        string guestToken = await GetAccessToken(guestEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var request = new ReserveBookingRequest(
            apartmentId,
            new DateOnly(2028, 2, 1),
            new DateOnly(2028, 2, 10))
        {
            ApartmentId = apartmentId,
            StartDate = new DateOnly(2028, 2, 1),
            EndDate = new DateOnly(2028, 2, 10)
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/bookings", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        // Verify the booking was created with Confirmed status (2)
        HttpResponseMessage getResponse = await HttpClient.GetAsync(response.Headers.Location);
        getResponse.EnsureSuccessStatusCode();
        var bookingResponse = await getResponse.Content.ReadFromJsonAsync<BookingResponse>();
        bookingResponse.Should().NotBeNull();
        bookingResponse.UserId.Should().Be(guestUserId);
        bookingResponse.Status.Should().Be((int)Domain.Bookings.BookingStatus.PendingPayment);
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn400_WhenMinimumNightsNotMet()
    {
        // Arrange
        var adminEmail = $"admin_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerAdminCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            adminEmail, "Admin", "User", password, new DateOnly(1990, 1, 1));
        await Sender.Send(registerAdminCommand);
        await PromoteToAdminAsync(adminEmail);

        string adminToken = await GetAccessToken(adminEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, adminToken);

        // MinimumNights = 3 for this apartment
        var aptData = ApartmentData.ValidCreateApartmentRequest with { MinimumNights = 3 };
        HttpResponseMessage aptResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>();

        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        await Sender.Send(registerGuestCommand);

        string guestToken = await GetAccessToken(guestEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Try to book for 2 nights (2028-01-01 to 2028-01-03), but minimum is 3
        var request = new ReserveBookingRequest(
            apartmentId,
            new DateOnly(2028, 1, 1),
            new DateOnly(2028, 1, 3))
        {
            ApartmentId = apartmentId,
            StartDate = new DateOnly(2028, 1, 1),
            EndDate = new DateOnly(2028, 1, 3)
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/bookings", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Detail.Should().Be(Domain.Bookings.BookingErrors.BelowMinimumNights.Name);
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn400_WhenCheckInCutOffNotMet()
    {
        // Arrange
        var adminEmail = $"admin_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerAdminCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            adminEmail, "Admin", "User", password, new DateOnly(1990, 1, 1));
        await Sender.Send(registerAdminCommand);
        await PromoteToAdminAsync(adminEmail);

        string adminToken = await GetAccessToken(adminEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, adminToken);

        // CheckInCutOffHours = 48 for this apartment.
        var aptData = ApartmentData.ValidCreateApartmentRequest;
        aptData = aptData with { CheckInCutOffHours = 48 };
        HttpResponseMessage aptResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>();

        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        await Sender.Send(registerGuestCommand);

        string guestToken = await GetAccessToken(guestEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        // We try to book starting tomorrow. Since CutOff is 48 hours (2 days), tomorrow is invalid.
        var today = DateTime.UtcNow;
        var tomorrow = DateOnly.FromDateTime(today.AddDays(1));
        var endDate = tomorrow.AddDays(4); // meets the 3 days min nights

        var request = new ReserveBookingRequest(
            apartmentId,
            tomorrow,
            endDate)
        {
            ApartmentId = apartmentId,
            StartDate = tomorrow,
            EndDate = endDate
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/bookings", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Detail.Should().Be(Domain.Bookings.BookingErrors.CheckInTooSoon.Name);
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn201_WhenBookingStartsTodayAndBeforeCutoff()
    {
        // Arrange
        var adminEmail = $"admin_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerAdminCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            adminEmail, "Admin", "User", password, new DateOnly(1990, 1, 1));
        await Sender.Send(registerAdminCommand);
        await PromoteToAdminAsync(adminEmail);

        string adminToken = await GetAccessToken(adminEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, adminToken);

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var hoursRemaining = 24 - now.Hour;
        
        // Cut-off must be before the remaining hours so that utcNow < cutOffLimit
        var cutOffHours = Math.Max(0, hoursRemaining - 2);

        var aptData = ApartmentData.ValidCreateApartmentRequest with { CheckInCutOffHours = cutOffHours };
        HttpResponseMessage aptResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>();

        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        await Sender.Send(registerGuestCommand);

        string guestToken = await GetAccessToken(guestEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var endDate = today.AddDays(3); // meets the 1 days min nights (or 3, we reserve 3 to be safe)
        var request = new ReserveBookingRequest(apartmentId, today, endDate)
        {
            ApartmentId = apartmentId,
            StartDate = today,
            EndDate = endDate
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/bookings", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn400_WhenBookingStartsTodayAndAfterCutoff()
    {
        // Arrange
        var adminEmail = $"admin_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerAdminCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            adminEmail, "Admin", "User", password, new DateOnly(1990, 1, 1));
        await Sender.Send(registerAdminCommand);
        await PromoteToAdminAsync(adminEmail);

        string adminToken = await GetAccessToken(adminEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, adminToken);

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var hoursRemaining = 24 - now.Hour;
        
        // Cut-off must be after the remaining hours so that utcNow > cutOffLimit
        var cutOffHours = Math.Min(48, hoursRemaining + 2);

        var aptData = ApartmentData.ValidCreateApartmentRequest with { CheckInCutOffHours = cutOffHours };
        HttpResponseMessage aptResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>();

        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        await Sender.Send(registerGuestCommand);

        string guestToken = await GetAccessToken(guestEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var endDate = today.AddDays(3);
        var request = new ReserveBookingRequest(apartmentId, today, endDate)
        {
            ApartmentId = apartmentId,
            StartDate = today,
            EndDate = endDate
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/bookings", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Detail.Should().Be(Domain.Bookings.BookingErrors.CheckInTooSoon.Name);
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn201_WhenBookingStartsTomorrowAndBeforeCutoff()
    {
        // Arrange
        var adminEmail = $"admin_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerAdminCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            adminEmail, "Admin", "User", password, new DateOnly(1990, 1, 1));
        await Sender.Send(registerAdminCommand);
        await PromoteToAdminAsync(adminEmail);

        string adminToken = await GetAccessToken(adminEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, adminToken);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        // Using default CheckInCutOffHours = 3. Tomorrow reservation is always way before tomorrow's cutoff if booked today.
        var aptData = ApartmentData.ValidCreateApartmentRequest with { CheckInCutOffHours = 3 };
        HttpResponseMessage aptResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>();

        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterUser.RegisterUserCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        await Sender.Send(registerGuestCommand);

        string guestToken = await GetAccessToken(guestEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var endDate = tomorrow.AddDays(3);
        var request = new ReserveBookingRequest(apartmentId, tomorrow, endDate)
        {
            ApartmentId = apartmentId,
            StartDate = tomorrow,
            EndDate = endDate
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/bookings", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
