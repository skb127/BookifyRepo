using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Bookings;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Bookings.RejectBooking;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Bookings;

public class RejectBookingTests : BaseIntegrationTest
{
    private static readonly Guid BookingId = Guid.CreateVersion7();
    private readonly MockEmailService _mockEmailService;
    private readonly MockPaymentGateway _mockPaymentGateway;

    public RejectBookingTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
        _mockEmailService = factory.MockEmailService;
        _mockPaymentGateway = factory.MockPaymentGateway;
    }

    [Fact]
    public async Task RejectBooking_ShouldReturnFailure_WhenBookingIsNotFound()
    {
        // Arrange
        var command = new RejectBookingCommand(BookingId, ReasonType.None, "Test rejection");

        // Act
        Result result = await Sender.Send(command);

        // Assert
        result.Error.Should().Be(BookingErrors.NotFound);
    }

    [Fact]
    public async Task RejectBooking_ShouldRejectBookingAndReleasePayment_WhenBookingIsReserved()
    {
        // Arrange
        var (_, _, bookingId, guestToken, guestEmail, hostEmail) =
            await BookingTestHelpers.SetupReservedBookingWithHostAsync(this);

        string hostToken = await GetAccessToken(hostEmail, "Password123!");

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            hostToken);

        var request = new BookingReasonRequest(ReasonType.None, "Rejecting booking");
        DateTime since = DateTime.UtcNow;

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            new Uri($"api/v1/bookings/{bookingId}/rejection", UriKind.Relative),
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify Rejection Email was Sent to guest (which waits for outbox processing)
        EmailMessage rejectionEmail =
            await _mockEmailService.WaitForEmailToAsync(guestEmail, "Booking Rejected", since: since);
        rejectionEmail.Subject.Should().Be("Booking Rejected");

        // Verify Booking Status & Payment Status
        var bookingDetails = await BookingTestHelpers.GetBookingViaApiAsync(this, bookingId, guestToken);
        bookingDetails.Should().NotBeNull();
        bookingDetails.Status.Should().Be((int)BookingStatus.Rejected);
        bookingDetails.PaymentStatus.Should().Be((int)PaymentStatus.AuthorizationReleased);

        // Verify Stripe Payment Release
        _mockPaymentGateway.CancelledPaymentIntents.Should().Contain($"intent_{bookingId}");

        // Verify Local Transaction Status is updated to "canceled"
        string adminToken = await GetAdminTokenAsync();
        var transactions = await BookingTestHelpers.GetBookingTransactionsViaApiAsync(this, bookingId, adminToken);
        transactions.Should().ContainSingle(t => t.ProviderStatus == "canceled");
    }
}
