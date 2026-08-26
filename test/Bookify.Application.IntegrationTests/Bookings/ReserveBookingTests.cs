using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Apartments.Requests;
using Bookify.Api.Controllers.Bookings.Requests;
using Bookify.Application.Bookings.GetBooking;
using Bookify.Application.IntegrationTests.Apartments;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Bookings;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Application.IntegrationTests.Bookings;

public class ReserveBookingTests : BaseIntegrationTest
{
    public ReserveBookingTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn201_WhenRequestIsValid()
    {
        // Arrange
        // 1. Create a host to create the apartment
        var hostEmail = $"host_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerHostCommand = new Bookify.Application.Users.RegisterHost.RegisterHostCommand(
            hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        await Sender.Send(registerHostCommand);

        // 2. Create apartment mapping to Host context
        string hostToken = await GetAccessToken(hostEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, hostToken);
        var aptData = ApartmentData.ValidCreateApartmentRequest;
        HttpResponseMessage aptResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>();

        // 3. Create a guest to reserve
        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
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
        bookingResponse.Status.Should().Be((int)BookingStatus.PendingPayment);
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
        // 1. Create a host to create the apartment
        var hostEmail = $"host_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerHostCommand = new Bookify.Application.Users.RegisterHost.RegisterHostCommand(
            hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        await Sender.Send(registerHostCommand);

        // 2. Create apartment mapping to Host context with InstantBooking = true
        string hostToken = await GetAccessToken(hostEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, hostToken);
        var aptData = ApartmentData.ValidCreateApartmentInstantBookingRequest;
        HttpResponseMessage aptResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>();

        // 3. Create a guest to reserve
        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
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
        bookingResponse.Status.Should().Be((int)BookingStatus.PendingPayment);
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn400_WhenMinimumNightsNotMet()
    {
        // Arrange
        var hostEmail = $"host_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerHostCommand = new Bookify.Application.Users.RegisterHost.RegisterHostCommand(
            hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        await Sender.Send(registerHostCommand);

        string hostToken = await GetAccessToken(hostEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, hostToken);

        // MinimumNights = 3 for this apartment
        var aptData = ApartmentData.ValidCreateApartmentRequest with { MinimumNights = 3 };
        HttpResponseMessage aptResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>();

        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
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
        problemDetails.Detail.Should().Be(BookingErrors.BelowMinimumNights.Name);
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn400_WhenCheckInCutOffNotMet()
    {
        // Arrange
        var hostEmail = $"host_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerHostCommand = new Bookify.Application.Users.RegisterHost.RegisterHostCommand(
            hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        await Sender.Send(registerHostCommand);

        string hostToken = await GetAccessToken(hostEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, hostToken);

        // CheckInCutOffHours = 48 for this apartment.
        var aptData = ApartmentData.ValidCreateApartmentRequest;
        aptData = aptData with { CheckInCutOffHours = 48 };
        HttpResponseMessage aptResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>();

        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
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
        problemDetails.Detail.Should().Be(BookingErrors.CheckInTooSoon.Name);
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn201_WhenBookingStartsTodayAndBeforeCutoff()
    {
        // Arrange
        var hostEmail = $"host_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerHostCommand = new Bookify.Application.Users.RegisterHost.RegisterHostCommand(
            hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        await Sender.Send(registerHostCommand);

        string hostToken = await GetAccessToken(hostEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, hostToken);

        // Freeze time at 10:00 AM UTC today.
        // With CheckInCutOffHours = 3 → cutOffLimit = today midnight+1day - 3h = today 21:00 UTC.
        // 10:00 < 21:00 → passes cutoff check. Deterministic regardless of real wall clock.
        var frozenNow = DateTime.UtcNow.Date.AddHours(10); // today at 10:00 AM UTC
        DateTimeProvider.SetUtcNow(frozenNow);

        var today = DateOnly.FromDateTime(frozenNow);

        var aptData = ApartmentData.ValidCreateApartmentRequest with
        {
            CheckInCutOffHours = 3,
            InstantBooking = false
        };
        HttpResponseMessage aptResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>();

        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
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
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn400_WhenBookingStartsTodayAndAfterCutoff()
    {
        // Arrange
        var hostEmail = $"host_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerHostCommand = new Bookify.Application.Users.RegisterHost.RegisterHostCommand(
            hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        await Sender.Send(registerHostCommand);

        string hostToken = await GetAccessToken(hostEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, hostToken);

        // Freeze time at 22:00 UTC today.
        // With CheckInCutOffHours = 3 → cutOffLimit = today midnight+1day - 3h = today 21:00 UTC.
        // 22:00 > 21:00 → fails cutoff check → CheckInTooSoon.
        var frozenNow = DateTime.UtcNow.Date.AddHours(22); // today 22:00 UTC
        DateTimeProvider.SetUtcNow(frozenNow);

        var today = DateOnly.FromDateTime(frozenNow);

        var aptData = ApartmentData.ValidCreateApartmentRequest with
        {
            CheckInCutOffHours = 3,
            InstantBooking = false
        };
        HttpResponseMessage aptResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>();

        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
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
        problemDetails.Detail.Should().Be(BookingErrors.CheckInTooSoon.Name);
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn201_WhenBookingStartsTomorrowAndBeforeCutoff()
    {
        // Arrange
        var hostEmail = $"host_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerHostCommand = new Bookify.Application.Users.RegisterHost.RegisterHostCommand(
            hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        await Sender.Send(registerHostCommand);

        string hostToken = await GetAccessToken(hostEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, hostToken);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        // Using default CheckInCutOffHours = 3. Tomorrow reservation is always way before tomorrow's cutoff if booked today.
        var aptData = ApartmentData.ValidCreateApartmentRequest with { CheckInCutOffHours = 3 };
        HttpResponseMessage aptResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>();

        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
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

    [Fact]
    public async Task ReserveBooking_WithActiveTaxRules_CreatesOneSnapshotPerRule()
    {
        // Arrange
        var hostEmail = $"host_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerHostCommand = new Bookify.Application.Users.RegisterHost.RegisterHostCommand(
            hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        await Sender.Send(registerHostCommand);

        string hostToken = await GetAccessToken(hostEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, hostToken);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var aptData = ApartmentData.ValidCreateApartmentRequest with
        {
            CheckInCutOffHours = 3,
            Address = new AddressRequest("US", "State", "ZipCode", "City", "Street")
        };
        HttpResponseMessage aptResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>();

        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        await Sender.Send(registerGuestCommand);

        string guestToken = await GetAccessToken(guestEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var endDate = tomorrow.AddDays(3); // 3 nights
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
        var bookingId = await response.Content.ReadFromJsonAsync<Guid>();

        // Verify with DbContext
        Booking? booking = await DbContext.Set<Booking>()
            .Include(b => b.Taxes)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        booking.Should().NotBeNull();
        booking.Taxes.Should().HaveCount(2); // Standard Tax 10% + Tourist Tax 5/night
    }

    [Fact]
    public async Task ReserveBooking_WithMultipleTaxRules_SnapshotAmountsAreCorrect()
    {
        // Arrange
        var hostEmail = $"host_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerHostCommand = new Bookify.Application.Users.RegisterHost.RegisterHostCommand(
            hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        await Sender.Send(registerHostCommand);

        string hostToken = await GetAccessToken(hostEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, hostToken);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var aptData = ApartmentData.ValidCreateApartmentRequest with
        {
            CheckInCutOffHours = 3,
            Address = new AddressRequest("US", "State", "ZipCode", "City", "Street")
        }; // Price = 100 USD
        HttpResponseMessage aptResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>();

        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        await Sender.Send(registerGuestCommand);

        string guestToken = await GetAccessToken(guestEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var endDate = tomorrow.AddDays(3); // 3 nights => Total price base = 3 nights * 100 USD = 300 USD
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
        var bookingId = await response.Content.ReadFromJsonAsync<Guid>();

        // Verify DTO fields from GET endpoint also
        HttpResponseMessage getResponse =
            await HttpClient.GetAsync(new Uri($"api/v1/bookings/{bookingId}", UriKind.Relative));
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        BookingResponse? bookingDto = await getResponse.Content.ReadFromJsonAsync<BookingResponse>();
        bookingDto.Should().NotBeNull();
        bookingDto.Taxes.Should().HaveCount(2);

        // Standard Tax 10%: 10% of 350 USD (300 USD base + 50 USD cleaning fee) = 35 USD
        BookingTaxResponse percentageTax =
            bookingDto.Taxes.Should().ContainSingle(t => t.TaxRuleName == "Standard Tax 10%").Subject;
        percentageTax.CalculatedAmount.Should().Be(35.00m);
        percentageTax.Currency.Should().Be("USD");

        // Tourist Tax 5/night: €5 * 3 nights = 15 USD
        BookingTaxResponse fixedTax = bookingDto.Taxes.Should()
            .ContainSingle(t => t.TaxRuleName == "Tourist Tax 5/night").Subject;
        fixedTax.CalculatedAmount.Should().Be(15.00m);
        fixedTax.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task ReserveBooking_ForUnknownCountry_SucceedsWithNoTaxes()
    {
        // Arrange
        var hostEmail = $"host_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerHostCommand = new Bookify.Application.Users.RegisterHost.RegisterHostCommand(
            hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        await Sender.Send(registerHostCommand);

        string hostToken = await GetAccessToken(hostEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, hostToken);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        // Use Country Code that has no active tax rules seeded: "Spain"
        var aptData = ApartmentData.ValidCreateApartmentRequest with
        {
            CheckInCutOffHours = 3,
            Address = new AddressRequest("Spain", "Madrid", "28001", "Madrid", "Gran Vía 12")
        };
        HttpResponseMessage aptResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>();

        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
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
        var bookingId = await response.Content.ReadFromJsonAsync<Guid>();

        // Verify with DbContext that taxes is empty
        Booking? booking = await DbContext.Set<Booking>()
            .Include(b => b.Taxes)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        booking.Should().NotBeNull();
        booking.Taxes.Should().BeEmpty();
    }

    [Fact]
    public async Task ReserveBooking_ShouldSetGuestCount_WhenProvidedInRequest()
    {
        // Arrange
        // 1. Create a host to create the apartment
        var hostEmail = $"host_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerHostCommand = new Bookify.Application.Users.RegisterHost.RegisterHostCommand(
            hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        await Sender.Send(registerHostCommand);

        // 2. Create apartment mapping to Host context
        string hostToken = await GetAccessToken(hostEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, hostToken);
        var aptData = ApartmentData.ValidCreateApartmentRequest with
        {
            BaseGuests = 2,
            MaxGuests = 6,
            ExtraGuestFee = new MoneyRequest(20.0m, "USD")
        };
        HttpResponseMessage aptResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>();

        // 3. Create a guest to reserve
        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        await Sender.Send(registerGuestCommand);

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
            EndDate = new DateOnly(2028, 1, 10),
            GuestCount = 3
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/bookings", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var bookingId = await response.Content.ReadFromJsonAsync<Guid>();

        var getResponse = await HttpClient.GetAsync(new Uri($"api/v1/bookings/{bookingId}", UriKind.Relative));
        getResponse.EnsureSuccessStatusCode();
        var bookingResponse = await getResponse.Content.ReadFromJsonAsync<BookingResponse>();
        bookingResponse.Should().NotBeNull();
        bookingResponse.GuestCount.Should().Be(3);
    }

    [Fact]
    public async Task ReserveBooking_ShouldReturn400_WhenGuestCountExceedsMax()
    {
        // Arrange
        // 1. Create a host to create the apartment
        var hostEmail = $"host_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerHostCommand = new Bookify.Application.Users.RegisterHost.RegisterHostCommand(
            hostEmail, "Host", "User", password, new DateOnly(1990, 1, 1), "+34612345678");
        await Sender.Send(registerHostCommand);

        // 2. Create apartment mapping to Host context
        string hostToken = await GetAccessToken(hostEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, hostToken);
        var aptData = ApartmentData.ValidCreateApartmentRequest with
        {
            BaseGuests = 1,
            MaxGuests = 2
        };
        HttpResponseMessage aptResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", aptData);
        aptResponse.EnsureSuccessStatusCode();
        var apartmentId = await aptResponse.Content.ReadFromJsonAsync<Guid>();

        // 3. Create a guest to reserve
        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var registerGuestCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        await Sender.Send(registerGuestCommand);

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
            EndDate = new DateOnly(2028, 1, 10),
            GuestCount = 3
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/bookings", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
