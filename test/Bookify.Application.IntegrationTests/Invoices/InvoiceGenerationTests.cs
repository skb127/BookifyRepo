using System.Net;
using System.Net.Http.Headers;
using Bookify.Application.Bookings.GetBooking;
using Bookify.Application.IntegrationTests.Apartments;
using Bookify.Application.IntegrationTests.Bookings;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Bookings;
using Bookify.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Application.IntegrationTests.Invoices;

public class InvoiceGenerationTests : BaseIntegrationTest
{
    public InvoiceGenerationTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task InvoiceGeneration_ShouldGeneratePdfAndAllowDownload_WhenInstantBookingPaid()
    {
        // Arrange
        var setup = await BookingTestHelpers.SetupBookingWithGeneratedInvoiceAsync(
            this,
            ApartmentData.InvoiceTestInstantApartmentRequest,
            guestCount: 2);

        using HttpClient client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, setup.guestToken);
        client.DefaultRequestHeaders.Add("X-Turnstile-Token", "XXXX.DUMMY.TOKEN.XXXX");

        // Act
        HttpResponseMessage response = await client.GetAsync(
            new Uri($"api/v1/bookings/{setup.bookingId}/invoices/{setup.invoiceId}/download", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("invoices");
    }

    [Fact]
    public async Task InvoiceGeneration_ShouldGeneratePdfAndAllowDownload_WhenManualBookingConfirmedAndPaid()
    {
        // Arrange
        var setup = await BookingTestHelpers.SetupBookingWithGeneratedInvoiceAsync(
            this,
            ApartmentData.InvoiceTestApartmentRequest,
            guestCount: 2);

        using HttpClient client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, setup.guestToken);
        client.DefaultRequestHeaders.Add("X-Turnstile-Token", "XXXX.DUMMY.TOKEN.XXXX");

        // Act
        HttpResponseMessage response = await client.GetAsync(
            new Uri($"api/v1/bookings/{setup.bookingId}/invoices/{setup.invoiceId}/download", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("invoices");
    }

    [Fact]
    public async Task InvoiceGeneration_ShouldHaveCorrectCalculations_WhenInstantBookingWithoutExtraGuests()
    {
        // Arrange: 9 nights (2027-01-01 to 2027-01-10), price $100/night, cleaning $25, amenities 6% ($54), 2 guests (0 extra)
        // Expected: PriceForPeriod = $900, Amenities = $54, Cleaning = $25, ExtraGuests = $0, TotalPrice = $979
        // Tax: 21% IVA = $205.59, TotalInvoice = $979 + $205.59 = $1184.59
        var setup = await BookingTestHelpers.SetupBookingWithGeneratedInvoiceAsync(
            this,
            ApartmentData.InvoiceTestInstantApartmentRequest,
            guestCount: 2,
            startDate: new DateOnly(2027, 1, 1),
            endDate: new DateOnly(2027, 1, 10));

        // Act 1: Verify booking pricing details via API endpoint
        BookingResponse booking = await BookingTestHelpers.GetBookingViaApiAsync(
            this, setup.bookingId, setup.guestToken);

        // Assert 1: Booking pricing breakdown
        booking.PriceAmount.Should().Be(900.0m);
        booking.PriceCurrency.Should().Be("USD");
        booking.CleaningFeeAmount.Should().Be(25.0m);
        booking.CleaningFeeCurrency.Should().Be("USD");
        booking.AmenitiesUpChargeAmount.Should().Be(54.0m);
        booking.AmenitiesUpChargeCurrency.Should().Be("USD");
        booking.ExtraGuestChargeAmount.Should().Be(0m);
        booking.TotalPriceAmount.Should().Be(979.0m);
        booking.TotalPriceCurrency.Should().Be("USD");
        booking.Taxes.Should().HaveCount(1);
        booking.Taxes[0].CalculatedAmount.Should().Be(205.59m);
        booking.Taxes[0].Currency.Should().Be("USD");

        // Act 2: Verify Invoice entity in database
        DbContext.ChangeTracker.Clear();
        Invoice invoice = await DbContext.Set<Invoice>()
            .AsNoTracking()
            .FirstAsync(i => i.Id == setup.invoiceId);

        // Assert 2: Invoice amounts and format
        invoice.TotalAmount.Should().Be(1184.59m);
        invoice.TaxAmount.Should().Be(205.59m);
        invoice.Currency.Should().Be("USD");
        invoice.InvoiceType.Should().Be(InvoiceType.Invoice);
        invoice.Status.Should().Be(InvoiceStatus.Generated);
        invoice.InvoiceNumber.Value.Should().Be($"INV-{setup.bookingId:N}".ToUpperInvariant());
        invoice.PdfBlobName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InvoiceGeneration_ShouldHaveCorrectCalculations_WhenBookingWithExtraGuests()
    {
        // Arrange: 9 nights (2027-01-01 to 2027-01-10), 4 guests (2 extra @ $15/night = $270)
        // Expected: PriceForPeriod = $900, Amenities = $54, Cleaning = $25, ExtraGuests = $270, TotalPrice = $1249
        // Tax: 21% IVA = $262.29, TotalInvoice = $1249 + $262.29 = $1511.29
        var setup = await BookingTestHelpers.SetupBookingWithGeneratedInvoiceAsync(
            this,
            ApartmentData.InvoiceTestInstantApartmentRequest,
            guestCount: 4,
            startDate: new DateOnly(2027, 1, 1),
            endDate: new DateOnly(2027, 1, 10));

        // Act 1: Verify booking pricing details via API endpoint
        BookingResponse booking = await BookingTestHelpers.GetBookingViaApiAsync(
            this, setup.bookingId, setup.guestToken);

        // Assert 1: Booking pricing breakdown with extra guests
        booking.PriceAmount.Should().Be(900.0m);
        booking.CleaningFeeAmount.Should().Be(25.0m);
        booking.AmenitiesUpChargeAmount.Should().Be(54.0m);
        booking.ExtraGuestChargeAmount.Should().Be(270.0m);
        booking.TotalPriceAmount.Should().Be(1249.0m);
        booking.Taxes.Should().HaveCount(1);
        booking.Taxes[0].CalculatedAmount.Should().Be(262.29m);

        // Act 2: Verify Invoice entity in database
        DbContext.ChangeTracker.Clear();
        Invoice invoice = await DbContext.Set<Invoice>()
            .AsNoTracking()
            .FirstAsync(i => i.Id == setup.invoiceId);

        // Assert 2: Invoice amounts include extra guests and taxes
        invoice.TotalAmount.Should().Be(1511.29m);
        invoice.TaxAmount.Should().Be(262.29m);
        invoice.Currency.Should().Be("USD");
        invoice.InvoiceType.Should().Be(InvoiceType.Invoice);
        invoice.Status.Should().Be(InvoiceStatus.Generated);
    }

    [Fact]
    public async Task InvoiceGeneration_ShouldBeIdempotent_WhenPaymentEventReprocessed()
    {
        // Arrange: Existing generated invoice
        var setup = await BookingTestHelpers.SetupBookingWithGeneratedInvoiceAsync(
            this,
            ApartmentData.InvoiceTestInstantApartmentRequest,
            guestCount: 2);

        // Act: Re-publish the same checkout.session.completed event to the Service Bus queue
        var duplicateEvent = new StripeWebhookEvent
        {
            EventType = "checkout.session.completed",
            BookingId = setup.bookingId,
            SessionId = $"session_{setup.bookingId}",
            PaymentIntentId = $"intent_{setup.bookingId}",
            IsInstant = true
        };

        await MessagePublisher.PublishAsync("stripe-events", duplicateEvent);

        // Wait briefly to allow the consumer to process the duplicate message
        await Task.Delay(2000);

        // Assert: Only 1 invoice exists for the booking
        DbContext.ChangeTracker.Clear();
        int invoiceCount = await DbContext.Set<Invoice>()
            .AsNoTracking()
            .CountAsync(i => i.BookingId == setup.bookingId && i.InvoiceType == InvoiceType.Invoice);

        invoiceCount.Should().Be(1);
    }
}
