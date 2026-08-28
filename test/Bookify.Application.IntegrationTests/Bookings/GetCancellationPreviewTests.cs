using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Bookings.Requests;
using Bookify.Application.Bookings.GetCancellationPreview;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Bookings;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Application.IntegrationTests.Bookings;

public class GetCancellationPreviewTests : BaseIntegrationTest
{
    public GetCancellationPreviewTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetCancellationPreview_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        var bookingId = Guid.NewGuid();

        // Act
        var response = await HttpClient.GetAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation-preview", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCancellationPreview_ShouldReturn404_WhenBookingNotFound()
    {
        // Arrange
        var bookingId = Guid.NewGuid();

        var guestEmail = $"guest_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerGuestCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        await Sender.Send(registerGuestCommand);

        string accessToken = await GetAccessToken(guestEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, accessToken);

        // Act
        var response = await HttpClient.GetAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation-preview", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCancellationPreview_ShouldReturn403_WhenUserUnauthorized()
    {
        // Arrange - Setup a booking for a guest and host
        var (_, _, bookingId, _, _, _) = await BookingTestHelpers.SetupReservedBookingWithHostAsync(this);

        // Register a completely different third user
        var otherEmail = $"other_{Guid.NewGuid()}@test.com";
        var password = "Password123!";
        var registerOtherCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
            otherEmail, "Other", "User", password, new DateOnly(1995, 5, 5));
        await Sender.Send(registerOtherCommand);

        string otherToken = await GetAccessToken(otherEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, otherToken);

        // Act - Call preview with other user token
        var response = await HttpClient.GetAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation-preview", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem.Title.Should().Be(BookingErrors.Unauthorized.Code);
    }

    [Fact]
    public async Task GetCancellationPreview_ShouldReturn200WithZeroPenalty_WhenBookingNotPaid()
    {
        // Arrange - Setup a reserved booking (which has Authorized payment, not Paid)
        var (_, _, bookingId, guestAccessToken, _, _) =
            await BookingTestHelpers.SetupReservedBookingWithHostAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestAccessToken);

        // Act
        var response = await HttpClient.GetAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation-preview", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var preview = await response.Content.ReadFromJsonAsync<CancellationPreviewResponse>();
        preview.Should().NotBeNull();
        preview.PenaltyAmount.Should().Be(0m);
        preview.RefundAmount.Should().Be(0m);
        preview.RequiresRefund.Should().BeFalse();
        preview.CancellationPolicyName.Should().Be("None");
        preview.IsCancelledByHost.Should().BeFalse();
    }

    [Fact]
    public async Task GetCancellationPreview_ShouldReturn200WithGuestEarlyDetails_WhenGuestCancelsPaidBookingEarly()
    {
        // Arrange - Setup a reserved booking
        var (_, _, bookingId, guestAccessToken, _, _) =
            await BookingTestHelpers.SetupReservedBookingWithHostAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestAccessToken);

        // Confirm the booking (transitions status to Confirmed, and payment status to Paid)
        HttpResponseMessage confirmResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/confirmation", UriKind.Relative),
            null);
        confirmResponse.EnsureSuccessStatusCode();

        // Wait for outbox handler to capture payment and mark PaymentStatus as Paid
        await PollingHelper.WaitUntilAsync(
            action: async () =>
            {
                DbContext.ChangeTracker.Clear();
                return await DbContext.Set<Booking>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == bookingId);
            },
            isReady: b => b is not null && b.PaymentStatus == PaymentStatus.Paid,
            timeout: TimeSpan.FromSeconds(15),
            interval: TimeSpan.FromMilliseconds(200));

        // Act
        var response = await HttpClient.GetAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation-preview", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var preview = await response.Content.ReadFromJsonAsync<CancellationPreviewResponse>();
        preview.Should().NotBeNull();
        preview.TotalPrice.Should().Be(950m); // Apartment price is 100/night * 9 nights = 900 + 50 cleaning = 950 USD
        // Guest Early Cancellation Penalty is 10% -> 95 USD
        preview.PenaltyAmount.Should().Be(95m);
        // Refund: 950 - 95 = 855 USD
        preview.RefundAmount.Should().Be(855m);
        preview.RequiresRefund.Should().BeTrue();
        preview.IsLateCancellation.Should().BeFalse(); // SetupReservedBookingWithHostAsync uses future dates (2027)
        preview.CancellationPolicyName.Should().Be("Default Cancellation Policy");
        preview.IsCancelledByHost.Should().BeFalse();
    }

    [Fact]
    public async Task GetCancellationPreview_ShouldReturn200WithGuestLateDetails_WhenGuestCancelsPaidBookingLate()
    {
        // Arrange - Setup a confirmed paid booking starting TOMORROW (so it's a late cancellation)
        var password = "Password123!";
        var (apartmentId, guestToken, _, _) =
            await BookingTestHelpers.SetupApartmentAndGuestWithHostAsync(this, password);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = today.AddDays(1); // tomorrow
        var endDate = today.AddDays(5); // 4 nights

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        var reserveRequest = new ReserveBookingRequest(
            apartmentId,
            startDate,
            endDate)
        {
            ApartmentId = apartmentId,
            StartDate = startDate,
            EndDate = endDate
        };

        HttpResponseMessage reserveResponse =
            await HttpClient.PostAsJsonAsync("api/v1/bookings", reserveRequest);
        reserveResponse.EnsureSuccessStatusCode();

        Guid bookingId = await reserveResponse.Content.ReadFromJsonAsync<Guid>();

        // Confirm payment (marks as Paid and Confirmed for instant booking)
        var confirmPaymentCommand = new Bookify.Application.Payments.ConfirmPayment.ConfirmPaymentCommand(
            bookingId, $"session_{bookingId}", $"intent_{bookingId}", IsInstantBooking: true);
        var confirmPaymentResult = await Sender.Send(confirmPaymentCommand);
        confirmPaymentResult.IsSuccess.Should().BeTrue();

        // Act
        var response = await HttpClient.GetAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation-preview", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var preview = await response.Content.ReadFromJsonAsync<CancellationPreviewResponse>();
        preview.Should().NotBeNull();
        // Total price = 4 nights * 100 + 50 cleaning = 450 USD.
        preview.TotalPrice.Should().Be(450m);
        // Late guest penalty is 50% = 225 USD.
        preview.PenaltyAmount.Should().Be(225m);
        // Refund: 450 - 225 = 225 USD
        preview.RefundAmount.Should().Be(225m);
        preview.RequiresRefund.Should().BeTrue();
        preview.IsLateCancellation.Should().BeTrue();
        preview.CancellationPolicyName.Should().Be("Default Cancellation Policy");
        preview.IsCancelledByHost.Should().BeFalse();
    }

    [Fact]
    public async Task GetCancellationPreview_ShouldReturn200WithHostEarlyDetails_WhenHostCancelsPaidBookingEarly()
    {
        // Arrange - Setup a confirmed paid booking (early cancellation)
        var (_, _, bookingId, _, _, hostEmail) =
            await BookingTestHelpers.SetupConfirmedPaidBookingWithHostAsync(this);

        // Authenticate as Host
        string hostAccessToken = await GetAccessToken(hostEmail, "Password123!");
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, hostAccessToken);

        // Act
        var response = await HttpClient.GetAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation-preview", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var preview = await response.Content.ReadFromJsonAsync<CancellationPreviewResponse>();
        preview.Should().NotBeNull();
        preview.TotalPrice.Should().Be(950m);
        // Early host penalty is 10% = 95 USD.
        preview.PenaltyAmount.Should().Be(95m);
        // For host cancellation, guest refund is 100% (950 USD).
        preview.RefundAmount.Should().Be(950m);
        preview.RequiresRefund.Should().BeTrue();
        preview.IsLateCancellation.Should().BeFalse();
        preview.CancellationPolicyName.Should().Be("Default Cancellation Policy");
        preview.IsCancelledByHost.Should().BeTrue();
    }

    [Fact]
    public async Task GetCancellationPreview_ShouldReturn200WithHostLateDetails_WhenHostCancelsPaidBookingLate()
    {
        // Arrange - Setup a confirmed paid booking starting TOMORROW (so it's a late cancellation)
        var password = "Password123!";
        var (apartmentId, guestToken, _, hostEmail) =
            await BookingTestHelpers.SetupApartmentAndGuestWithHostAsync(this, password);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = today.AddDays(1); // tomorrow
        var endDate = today.AddDays(5); // 4 nights

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        var reserveRequest = new ReserveBookingRequest(
            apartmentId,
            startDate,
            endDate)
        {
            ApartmentId = apartmentId,
            StartDate = startDate,
            EndDate = endDate
        };

        HttpResponseMessage reserveResponse =
            await HttpClient.PostAsJsonAsync("api/v1/bookings", reserveRequest);
        reserveResponse.EnsureSuccessStatusCode();

        Guid bookingId = await reserveResponse.Content.ReadFromJsonAsync<Guid>();

        // Confirm payment (marks as Paid and Confirmed for instant booking)
        var confirmPaymentCommand = new Bookify.Application.Payments.ConfirmPayment.ConfirmPaymentCommand(
            bookingId, $"session_{bookingId}", $"intent_{bookingId}", IsInstantBooking: true);
        var confirmPaymentResult = await Sender.Send(confirmPaymentCommand);
        confirmPaymentResult.IsSuccess.Should().BeTrue();

        // Authenticate as Host
        string hostAccessToken = await GetAccessToken(hostEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, hostAccessToken);

        // Act
        var response = await HttpClient.GetAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation-preview", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var preview = await response.Content.ReadFromJsonAsync<CancellationPreviewResponse>();
        preview.Should().NotBeNull();
        // Total price = 4 nights * 100 + 50 cleaning = 450 USD.
        preview.TotalPrice.Should().Be(450m);
        // Late host penalty is 50% = 225 USD.
        preview.PenaltyAmount.Should().Be(225m);
        // Guest refund is 100% = 450 USD.
        preview.RefundAmount.Should().Be(450m);
        preview.RequiresRefund.Should().BeTrue();
        preview.IsLateCancellation.Should().BeTrue();
        preview.CancellationPolicyName.Should().Be("Default Cancellation Policy");
        preview.IsCancelledByHost.Should().BeTrue();
    }
}
