using System.Net;
using System.Net.Http.Headers;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Bookings.ConfirmBooking;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Bookings;

public class ConfirmBookingTests : BaseIntegrationTest
{
    private static readonly Guid BookingId = Guid.CreateVersion7();
    private readonly MockPaymentGateway _mockPaymentGateway;
    private readonly MockEmailService _mockEmailService;

    public ConfirmBookingTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
        _mockPaymentGateway = factory.MockPaymentGateway;
        _mockEmailService = factory.MockEmailService;
    }

    [Fact]
    public async Task ConfirmBooking_ShouldReturnFailure_WhenBookingIsNotFound()
    {
        // Arrange
        var command = new ConfirmBookingCommand(BookingId);

        // Act
        Result result = await Sender.Send(command);

        // Assert
        result.Error.Should().Be(BookingErrors.NotFound);
    }

    [Fact]
    public async Task ConfirmBooking_ShouldConfirmBookingAndCapturePayment_WhenBookingIsReserved()
    {
        // Arrange
        var (_, _, bookingId, guestToken, guestEmail, hostEmail) = 
            await BookingTestHelpers.SetupReservedBookingWithHostAsync(this);

        string hostToken = await GetAccessToken(hostEmail, "Password123!");

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            hostToken);

        DateTime since = DateTime.UtcNow;

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync(
            new Uri($"api/v1/bookings/{bookingId}/confirmation", UriKind.Relative),
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Wait for Outbox processing to complete (via email sending check)
        EmailMessage confirmationEmail = await _mockEmailService.WaitForEmailToAsync(guestEmail, "Booking Confirmed", since: since);
        confirmationEmail.Subject.Should().Be("Booking Confirmed");

        // Verify Booking Status & Payment Status
        var bookingDetails = await BookingTestHelpers.GetBookingViaApiAsync(this, bookingId, guestToken);
        bookingDetails.Should().NotBeNull();
        bookingDetails.Status.Should().Be((int)BookingStatus.Confirmed);
        bookingDetails.PaymentStatus.Should().Be((int)PaymentStatus.Paid);

        // Verify Stripe Payment Capture
        _mockPaymentGateway.CapturedPaymentIntents.Should().Contain($"intent_{bookingId}");

        // Verify Local Transaction Status is updated to "paid"
        var transactions = await BookingTestHelpers.GetBookingTransactionsViaApiAsync(this, bookingId, hostToken);
        transactions.Should().ContainSingle(t => t.ProviderStatus == "paid");
    }
}
