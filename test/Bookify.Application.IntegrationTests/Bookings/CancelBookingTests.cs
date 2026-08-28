using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Bookings.Requests;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Bookings;
using Dapper;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Application.IntegrationTests.Bookings;

public class CancelBookingTests : BaseIntegrationTest
{
    private readonly MockEmailService _mockEmailService;
    private readonly MockPaymentGateway _mockPaymentGateway;

    public CancelBookingTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
        _mockEmailService = factory.MockEmailService;
        _mockPaymentGateway = factory.MockPaymentGateway;
    }

    [Fact]
    public async Task CancelBooking_ShouldReturnFailure_WhenBookingIsNotFound()
    {
        // Arrange
        var bookingId = Guid.CreateVersion7();

        // 1. Create a dummy user just to be able to authenticate and call the cancellation endpoint
        var guestEmail = $"guest_{Guid.CreateVersion7()}@test.com";
        var password = "Password123!";

        var registerGuestCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
            guestEmail, "Guest", "User", password, new DateOnly(1995, 5, 5));
        _ = await Sender.Send(registerGuestCommand);

        string accessToken = await GetAccessToken(guestEmail, password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelBooking_ShouldReturnFailure_WhenBookingIsInvalidForCancellation()
    {
        // Arrange - Setup apartment and booking with owner and guest tokens
        var (_, bookingId, ownerToken, _, guestToken, _) = await BookingTestHelpers.SetupApartmentWithOwnerAsync(this);

        // Reject the booking using the rejection endpoint and the owner's token
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            ownerToken);

        var rejectResponse = await HttpClient.PutAsJsonAsync(
            new Uri($"api/v1/bookings/{bookingId}/rejection", UriKind.Relative),
            new BookingReasonRequest(ReasonType.None, "Test rejection"));
        rejectResponse.EnsureSuccessStatusCode();

        // Now authenticate as the guest
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            guestToken);

        // Act - Attempt to cancel a rejected (invalid) booking
        HttpResponseMessage response = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Verify it was a NotCancellable error
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be(BookingErrors.NotCancellable.Code);
    }

    [Fact]
    public async Task CancelBooking_ShouldSucceed_WhenBookingIsConfirmed()
    {
        // Arrange
        var (_, _, bookingId, accessToken, guestEmail, hostEmail) =
            await BookingTestHelpers.SetupReservedBookingWithHostAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Confirm the booking
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

        DateTime since = DateTime.UtcNow;

        // Act - Cancel the confirmed booking
        HttpResponseMessage cancelResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation", UriKind.Relative),
            null);

        // Assert Cancellation Success
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify Status is Cancelled
        var bookingDetails = await BookingTestHelpers.GetBookingViaApiAsync(this, bookingId, accessToken);

        bookingDetails.Should().NotBeNull();
        bookingDetails.Status.Should().Be((int)BookingStatus.Cancelled);

        // Verify Email was Sent to guest
        EmailMessage cancellationEmail =
            await _mockEmailService.WaitForEmailToAsync(guestEmail, "Booking Cancelled", since: since);
        cancellationEmail.Subject.Should().Be("Booking Cancelled");

        // Verify Email was Sent to host
        EmailMessage hostCancellationEmail =
            await _mockEmailService.WaitForEmailToAsync(hostEmail, "Booking Cancelled by Guest", since: since);
        hostCancellationEmail.Subject.Should().Be("Booking Cancelled by Guest");

        // Verify Stripe refund was initiated: 950.0m USD total, early guest penalty is 10% (95.0m), refund is 90% (855.0m)
        _mockPaymentGateway.RefundRequests.Should().ContainSingle(r =>
            r.PaymentIntentId == $"intent_{bookingId}" && r.Amount == 855.0m && r.Currency == "USD");

        // Verify host balances
        await using var connection = DbContext.Database.GetDbConnection();
        var guestPenalty = await connection.QuerySingleOrDefaultAsync<decimal>(
            "SELECT amount FROM host_balances WHERE booking_id = @BookingId AND reason = 'guest_penalty'",
            new { BookingId = bookingId });
        guestPenalty.Should().Be(95.0m);

        var hostCompensationCount = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(1) FROM host_balances WHERE booking_id = @BookingId AND reason = 'host_compensation'",
            new { BookingId = bookingId });
        hostCompensationCount.Should().Be(0);
    }

    [Fact]
    public async Task CancelBooking_ShouldSucceedSilently_WhenBookingIsUnpaid()
    {
        // Arrange - Setup a booking in PendingPayment (Unpaid)
        var (_, _, bookingId, accessToken, guestEmail, hostEmail) =
            await BookingTestHelpers.SetupPendingPaymentBookingWithHostAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        DateTime since = DateTime.UtcNow;

        // Act - Cancel the unpaid booking
        HttpResponseMessage cancelResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation", UriKind.Relative),
            null);

        // Assert Cancellation Success
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify Status is Cancelled
        var bookingDetails = await BookingTestHelpers.GetBookingViaApiAsync(this, bookingId, accessToken);

        bookingDetails.Should().NotBeNull();
        bookingDetails.Status.Should().Be((int)BookingStatus.Cancelled);

        // Verify NO Cancellation Email was Sent to guest or host
        await _mockEmailService.EnsureNoEmailToAsync(guestEmail, "Booking Cancelled", durationMs: 2000, since: since);
        await _mockEmailService.EnsureNoEmailToAsync(hostEmail, "Booking Cancelled by Guest", durationMs: 2000, since: since);
    }

    [Fact]
    public async Task CancelBooking_ShouldFail_WhenHostCancelsBookingInPendingPaymentStatus()
    {
        // Arrange - Setup a booking in PendingPayment (Unpaid)
        var (_, _, bookingId, _, _, hostEmail) =
            await BookingTestHelpers.SetupPendingPaymentBookingWithHostAsync(this);

        string password = "Password123!";
        string hostAccessToken = await GetAccessToken(hostEmail, password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            hostAccessToken);

        // Act - Host cancels the unpaid booking
        HttpResponseMessage cancelResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation", UriKind.Relative),
            null);

        // Assert - Should return 400 Bad Request
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CancelBooking_ShouldSucceedAndReleaseAuthorization_WhenBookingIsAuthorized()
    {
        // Arrange
        var (_, _, bookingId, accessToken, guestEmail, hostEmail) =
            await BookingTestHelpers.SetupReservedBookingWithHostAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        DateTime since = DateTime.UtcNow;

        // Act - Cancel the authorized booking
        HttpResponseMessage cancelResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation", UriKind.Relative),
            null);

        // Assert Cancellation Success
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify Status is Cancelled
        var bookingDetails = await BookingTestHelpers.GetBookingViaApiAsync(this, bookingId, accessToken);

        bookingDetails.Should().NotBeNull();
        bookingDetails.Status.Should().Be((int)BookingStatus.Cancelled);

        // Verify Email was Sent to guest (since it was authorized and cancelled)
        EmailMessage cancellationEmail =
            await _mockEmailService.WaitForEmailToAsync(guestEmail, "Booking Cancelled", since: since);
        cancellationEmail.Subject.Should().Be("Booking Cancelled");

        // Verify Email was Sent to host
        EmailMessage hostCancellationEmail =
            await _mockEmailService.WaitForEmailToAsync(hostEmail, "Booking Cancelled by Guest", since: since);
        hostCancellationEmail.Subject.Should().Be("Booking Cancelled by Guest");

        // Verify Stripe cancellation was initiated
        _mockPaymentGateway.CancelledPaymentIntents.Should().Contain($"intent_{bookingId}");

        // Verify no host balances are recorded (since it was unpaid/authorized only)
        await using var connection = DbContext.Database.GetDbConnection();
        var hostBalanceCount = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(1) FROM host_balances WHERE booking_id = @BookingId",
            new { BookingId = bookingId });
        hostBalanceCount.Should().Be(0);
    }

    [Fact]
    public async Task CancelBooking_ShouldSucceedAndInitiateRefund_WhenGuestCancelsPaidBooking()
    {
        // Arrange - Setup a confirmed paid booking (starts in 2027, so it's early cancellation)
        var (_, _, bookingId, accessToken, guestEmail, hostEmail) =
            await BookingTestHelpers.SetupConfirmedPaidBookingWithHostAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        DateTime since = DateTime.UtcNow;

        // Act - Cancel the paid booking
        HttpResponseMessage cancelResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation", UriKind.Relative),
            null);

        // Assert Cancellation Success
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Wait for background outbox processor to run and transition the booking payment status to RefundProcessing
        bool isRefundProcessing = false;
        for (int i = 0; i < 30; i++)
        {
            var booking = await BookingTestHelpers.GetBookingViaApiAsync(this, bookingId, accessToken);
            if (booking.PaymentStatus == (int)PaymentStatus.RefundProcessing)
            {
                isRefundProcessing = true;
                break;
            }

            await Task.Delay(500);
        }

        isRefundProcessing.Should()
            .BeTrue(
                "The outbox processor should transition payment status to RefundProcessing after initiating the refund");

        // Verify Email was Sent to guest
        EmailMessage cancellationEmail =
            await _mockEmailService.WaitForEmailToAsync(guestEmail, "Booking Cancelled", since: since);
        cancellationEmail.Subject.Should().Be("Booking Cancelled");

        // Verify Email was Sent to host
        EmailMessage hostCancellationEmail =
            await _mockEmailService.WaitForEmailToAsync(hostEmail, "Booking Cancelled by Guest", since: since);
        hostCancellationEmail.Subject.Should().Be("Booking Cancelled by Guest");

        // Verify Stripe refund was initiated: 950.0m USD total, early guest penalty is 10% (95.0m), refund is 90% (855.0m)
        string adminAccessToken = await GetAdminTokenAsync();
        var transactions =
            await BookingTestHelpers.GetBookingTransactionsViaApiAsync(this, bookingId, adminAccessToken);
        var stripePaymentIntentId = transactions.Count > 0 ? transactions[0].StripePaymentIntentId : null;

        _mockPaymentGateway.RefundRequests.Should().ContainSingle(r =>
            r.PaymentIntentId == stripePaymentIntentId && r.Amount == 855.0m && r.Currency == "USD");

        await using var connection = DbContext.Database.GetDbConnection();

        // Verify host balances
        var guestPenalty = await connection.QuerySingleOrDefaultAsync<decimal>(
            "SELECT amount FROM host_balances WHERE booking_id = @BookingId AND reason = 'guest_penalty'",
            new { BookingId = bookingId });
        guestPenalty.Should().Be(95.0m);

        var hostCompensationCount = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(1) FROM host_balances WHERE booking_id = @BookingId AND reason = 'host_compensation'",
            new { BookingId = bookingId });
        hostCompensationCount.Should().Be(0);
    }

    [Fact]
    public async Task CancelBooking_ShouldSucceedAndApplyHostPenalty_WhenHostCancelsPaidBooking()
    {
        // Arrange - Setup a confirmed paid booking (early cancellation)
        var (_, _, bookingId, guestToken, guestEmail, hostEmail) =
            await BookingTestHelpers.SetupConfirmedPaidBookingWithHostAsync(this);

        // Authenticate as Host
        string hostAccessToken = await GetAccessToken(hostEmail, "Password123!");
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            hostAccessToken);

        DateTime since = DateTime.UtcNow;

        // Act - Host cancels the booking via HTTP PUT
        HttpResponseMessage cancelResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation", UriKind.Relative),
            null);

        // Assert
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify booking status is Cancelled
        var booking = await BookingTestHelpers.GetBookingViaApiAsync(this, bookingId, guestToken);
        booking.Should().NotBeNull();
        booking.Status.Should().Be((int)BookingStatus.Cancelled);

        // Verify host penalty was recorded in host_balances (95.0 USD) - 10% early host penalty rate for 950 USD total price
        await using var connection = DbContext.Database.GetDbConnection();
        var balance = await connection.QuerySingleOrDefaultAsync<decimal>(
            "SELECT amount FROM host_balances WHERE booking_id = @BookingId AND reason = 'host_compensation'",
            new { BookingId = bookingId });
        balance.Should().Be(95.0m);

        // Wait for background outbox processor to run and transition the booking payment status to RefundProcessing
        bool isRefundProcessing = false;
        for (int i = 0; i < 30; i++)
        {
            var b = await BookingTestHelpers.GetBookingViaApiAsync(this, bookingId, guestToken);
            if (b.PaymentStatus == (int)PaymentStatus.RefundProcessing)
            {
                isRefundProcessing = true;
                break;
            }

            await Task.Delay(500);
        }

        isRefundProcessing.Should()
            .BeTrue(
                "The outbox processor should transition payment status to RefundProcessing after initiating the refund");

        EmailMessage guestEmailMsg =
            await _mockEmailService.WaitForEmailToAsync(guestEmail, "Booking Cancelled by Host", since: since);
        guestEmailMsg.Subject.Should().Be("Booking Cancelled by Host");

        EmailMessage hostEmailMsg =
            await _mockEmailService.WaitForEmailToAsync(hostEmail, "Booking Cancelled - Penalty Applied", since: since);
        hostEmailMsg.Subject.Should().Be("Booking Cancelled - Penalty Applied");

        // Verify Stripe refund was initiated: 950.0m USD total, host cancels early so guest is fully refunded (950.0m)
        string adminTokenForTx = await GetAdminTokenAsync();
        var transactions = await BookingTestHelpers.GetBookingTransactionsViaApiAsync(this, bookingId, adminTokenForTx);
        var stripePaymentIntentId = transactions.Count > 0 ? transactions[0].StripePaymentIntentId : null;

        _mockPaymentGateway.RefundRequests.Should().ContainSingle(r =>
            r.PaymentIntentId == stripePaymentIntentId && r.Amount == 950.0m && r.Currency == "USD");

        // Verify guest penalty is 0
        var guestPenalty = await connection.QuerySingleOrDefaultAsync<decimal>(
            "SELECT amount FROM host_balances WHERE booking_id = @BookingId AND reason = 'guest_penalty'",
            new { BookingId = bookingId });
        guestPenalty.Should().Be(0);
    }

    [Fact]
    public async Task CancelBooking_ShouldSucceedAndApplyHostPenalty_WhenHostCancelsPaidBookingLate()
    {
        // Arrange - Setup a confirmed paid booking starting TOMORROW (so it's a late cancellation)
        var password = "Password123!";
        var (apartmentId, guestToken, guestEmail, hostEmail) =
            await BookingTestHelpers.SetupApartmentAndGuestWithHostAsync(this, password);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = today.AddDays(1); // tomorrow
        var endDate = today.AddDays(5);

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

        DateTime since = DateTime.UtcNow;

        // Act - Host cancels via HTTP
        HttpResponseMessage cancelResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation", UriKind.Relative),
            null);

        // Assert
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify booking status is Cancelled
        var booking = await BookingTestHelpers.GetBookingViaApiAsync(this, bookingId, guestToken);
        booking.Should().NotBeNull();
        booking.Status.Should().Be((int)BookingStatus.Cancelled);

        // Late host penalty rate is 50% = 225 USD.
        // Penalty should be recorded in host_balances under reason 'host_compensation'
        await using var connection = DbContext.Database.GetDbConnection();
        var hostPenalty = await connection.QuerySingleOrDefaultAsync<decimal>(
            "SELECT amount FROM host_balances WHERE booking_id = @BookingId AND reason = 'host_compensation'",
            new { BookingId = bookingId });
        hostPenalty.Should().Be(225.0m);


        // Verify Emails were Sent
        EmailMessage guestCancellationEmail =
            await _mockEmailService.WaitForEmailToAsync(guestEmail, "Booking Cancelled by Host", since: since);
        guestCancellationEmail.Subject.Should().Be("Booking Cancelled by Host");

        EmailMessage hostCancellationEmail =
            await _mockEmailService.WaitForEmailToAsync(hostEmail, "Booking Cancelled - Penalty Applied", since: since);
        hostCancellationEmail.Subject.Should().Be("Booking Cancelled - Penalty Applied");

        // Verify Stripe refund was initiated: guest gets 100% refund of 450.0m USD
        _mockPaymentGateway.RefundRequests.Should().ContainSingle(r =>
            r.PaymentIntentId == $"intent_{bookingId}" && r.Amount == 450.0m && r.Currency == "USD");

        // Verify guest penalty is 0
        var guestPenalty = await connection.QuerySingleOrDefaultAsync<decimal>(
            "SELECT amount FROM host_balances WHERE booking_id = @BookingId AND reason = 'guest_penalty'",
            new { BookingId = bookingId });
        guestPenalty.Should().Be(0);
    }

    [Fact]
    public async Task CancelBooking_ShouldReturnForbidden_WhenUnauthorizedUserCancelsBooking()
    {
        // Arrange - Setup a confirmed paid booking (Guest A, Host B)
        var (_, _, bookingId, _, _, _) =
            await BookingTestHelpers.SetupConfirmedPaidBookingWithHostAsync(this);

        // Create Guest C (unauthorized user)
        var unauthorizedEmail = $"stranger_{Guid.CreateVersion7()}@test.com";
        var password = "Password123!";
        var registerCommand = new Bookify.Application.Users.RegisterGuest.RegisterGuestCommand(
            unauthorizedEmail, "Stranger", "User", password, new DateOnly(1990, 1, 1));
        var registerResult = await Sender.Send(registerCommand);
        registerResult.IsSuccess.Should().BeTrue();

        string unauthorizedToken = await GetAccessToken(unauthorizedEmail, password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            unauthorizedToken);

        // Act - Attempt to cancel using unauthorized Guest C token
        HttpResponseMessage cancelResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation", UriKind.Relative),
            null);

        // Assert
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var problemDetails = await cancelResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be(BookingErrors.Unauthorized.Code);
    }


    [Fact]
    public async Task CancelBooking_ShouldSucceedAndApplyGuestPenalty_WhenGuestCancelsPaidBookingLate()
    {
        // Arrange - Setup a confirmed paid booking starting TOMORROW (so it's a late cancellation)
        var password = "Password123!";
        var (apartmentId, guestToken, guestEmail, hostEmail) =
            await BookingTestHelpers.SetupApartmentAndGuestWithHostAsync(this, password);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = today.AddDays(1); // tomorrow
        var endDate = today.AddDays(5);

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

        // Confirm payment (which marks as Paid and Confirmed for instant booking)
        var confirmPaymentCommand = new Bookify.Application.Payments.ConfirmPayment.ConfirmPaymentCommand(
            bookingId, $"session_{bookingId}", $"intent_{bookingId}", IsInstantBooking: true);
        var confirmPaymentResult = await Sender.Send(confirmPaymentCommand);
        confirmPaymentResult.IsSuccess.Should().BeTrue();

        DateTime since = DateTime.UtcNow;

        // Act - Cancel as guest
        HttpResponseMessage cancelResponse = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation", UriKind.Relative),
            null);

        // Assert
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify booking status is Cancelled
        var booking = await BookingTestHelpers.GetBookingViaApiAsync(this, bookingId, guestToken);
        booking.Should().NotBeNull();
        booking.Status.Should().Be((int)BookingStatus.Cancelled);

        // Total price = 4 nights * 100 + 50 cleaning = 450 USD.
        // Late guest penalty is 50% = 225 USD.
        // Penalty should be recorded in host_balances under reason 'guest_penalty'
        await using var connection = DbContext.Database.GetDbConnection();
        var guestPenalty = await connection.QuerySingleOrDefaultAsync<decimal>(
            "SELECT amount FROM host_balances WHERE booking_id = @BookingId AND reason = 'guest_penalty'",
            new { BookingId = bookingId });
        guestPenalty.Should().Be(225.0m);

        // Verify Emails were Sent
        EmailMessage guestCancellationEmail =
            await _mockEmailService.WaitForEmailToAsync(guestEmail, "Booking Cancelled", since: since);
        guestCancellationEmail.Subject.Should().Be("Booking Cancelled");

        EmailMessage hostCancellationEmail =
            await _mockEmailService.WaitForEmailToAsync(hostEmail, "Booking Cancelled by Guest", since: since);
        hostCancellationEmail.Subject.Should().Be("Booking Cancelled by Guest");

        // Verify Stripe refund was initiated: guest gets 50% refund (225.0m)
        _mockPaymentGateway.RefundRequests.Should().ContainSingle(r =>
            r.PaymentIntentId == $"intent_{bookingId}" && r.Amount == 225.0m && r.Currency == "USD");

        // Verify host penalty/compensation is 0
        var hostCompensationCount = await connection.QuerySingleAsync<int>(
            "SELECT COUNT(1) FROM host_balances WHERE booking_id = @BookingId AND reason = 'host_compensation'",
            new { BookingId = bookingId });
        hostCompensationCount.Should().Be(0);
    }

    [Fact]
    public async Task CancelBooking_ShouldReturnFailure_WhenBookingHasAlreadyStarted()
    {
        // Arrange
        var password = "Password123!";
        var (apartmentId, guestToken) = await BookingTestHelpers.SetupApartmentAndGuestAsync(this, password);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = today.AddDays(1); // Future booking to pass reserve validations
        var endDate = today.AddDays(5);

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

        // Shift the booking dates to the past in the database so it has already started
        var dbBooking = await DbContext.Set<Booking>().FirstAsync(b => b.Id == bookingId);
        typeof(Booking).GetProperty(nameof(Booking.Duration))!.SetValue(dbBooking,
            DateRange.Create(today.AddDays(-2), today.AddDays(5)));
        await DbContext.SaveChangesAsync();

        // Confirm payment (which marks as Paid and Confirmed for instant booking)
        var confirmPaymentCommand = new Bookify.Application.Payments.ConfirmPayment.ConfirmPaymentCommand(
            bookingId, $"session_{bookingId}", $"intent_{bookingId}", IsInstantBooking: true);
        var confirmPaymentResult = await Sender.Send(confirmPaymentCommand);
        confirmPaymentResult.IsSuccess.Should().BeTrue();

        // Act - Attempt to cancel the already started booking
        HttpResponseMessage response = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/cancellation", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Title.Should().Be(BookingErrors.AlreadyStarted.Code);
    }
}
