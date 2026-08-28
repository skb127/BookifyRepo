using System.Net;
using System.Net.Http.Headers;
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

public class CreditNoteGenerationTests : BaseIntegrationTest
{
    public CreditNoteGenerationTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CreditNoteGeneration_ShouldGeneratePdfAndAllowDownload_WhenHostCancelsAndRefundCompleted()
    {
        // Arrange: Host cancellation gives 100% refund of total price ($979 + $205.59 tax = $1184.59)
        var setup = await BookingTestHelpers.SetupBookingWithCreditNoteAsync(
            this,
            cancelledByHost: true,
            apartmentRequest: ApartmentData.InvoiceTestInstantApartmentRequest);

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
            new Uri($"api/v1/bookings/{setup.bookingId}/invoices/{setup.creditNoteId}/download", UriKind.Relative));

        // Assert 1: Download redirect
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("invoices");

        // Assert 2: Database record validation
        DbContext.ChangeTracker.Clear();
        Invoice creditNote = await DbContext.Set<Invoice>()
            .AsNoTracking()
            .FirstAsync(i => i.Id == setup.creditNoteId);

        creditNote.InvoiceType.Should().Be(InvoiceType.CreditNote);
        creditNote.Status.Should().Be(InvoiceStatus.Generated);
        creditNote.TotalAmount.Should().Be(1184.59m);
        creditNote.TaxAmount.Should().Be(205.59m);
        creditNote.OriginalInvoiceId.Should().Be(setup.invoiceId);
        creditNote.InvoiceNumber.Value.Should().Be($"CN-{setup.bookingId:N}".ToUpperInvariant());
        creditNote.PdfBlobName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreditNoteGeneration_ShouldCalculateProportionalTaxes_WhenGuestCancelsEarlyAndPartialRefundCompleted()
    {
        // Arrange: Guest early cancellation (>48h before check-in) -> 10% penalty -> 90% refund of ($979 + $205.59) = $1,066.13
        // originalTotalAmount = 979 + 205.59 = 1184.59
        // refundAmount = 1066.13
        // proportionalTaxAmount = Math.Round(205.59 * (1066.13 / 1184.59), 4) ~ 185.031
        var setup = await BookingTestHelpers.SetupBookingWithCreditNoteAsync(
            this,
            cancelledByHost: false,
            apartmentRequest: ApartmentData.InvoiceTestInstantApartmentRequest);

        // Act
        DbContext.ChangeTracker.Clear();
        Invoice creditNote = await DbContext.Set<Invoice>()
            .AsNoTracking()
            .FirstAsync(i => i.Id == setup.creditNoteId);

        // Assert: Proportional amounts
        creditNote.TotalAmount.Should().Be(1066.13m);
        creditNote.TaxAmount.Should().BeInRange(185.03m, 185.04m);
        creditNote.InvoiceType.Should().Be(InvoiceType.CreditNote);
        creditNote.Status.Should().Be(InvoiceStatus.Generated);
        creditNote.OriginalInvoiceId.Should().Be(setup.invoiceId);
    }

    [Fact]
    public async Task CreditNoteGeneration_ShouldReferenceOriginalInvoice()
    {
        // Arrange
        var setup = await BookingTestHelpers.SetupBookingWithCreditNoteAsync(
            this,
            cancelledByHost: true,
            apartmentRequest: ApartmentData.InvoiceTestInstantApartmentRequest);

        // Act
        DbContext.ChangeTracker.Clear();
        Invoice creditNote = await DbContext.Set<Invoice>()
            .AsNoTracking()
            .FirstAsync(i => i.Id == setup.creditNoteId);

        // Assert
        creditNote.OriginalInvoiceId.Should().NotBeNull();
        creditNote.OriginalInvoiceId.Should().Be(setup.invoiceId);
        creditNote.InvoiceNumber.Value.Should().Be($"CN-{setup.bookingId:N}".ToUpperInvariant());
    }

    [Fact]
    public async Task CreditNoteGeneration_ShouldBeIdempotent_WhenRefundEventReprocessed()
    {
        // Arrange
        var setup = await BookingTestHelpers.SetupBookingWithCreditNoteAsync(
            this,
            cancelledByHost: true,
            apartmentRequest: ApartmentData.InvoiceTestInstantApartmentRequest);

        // Act: Re-publish charge.refunded event
        var refundWebhookEvent = new StripeWebhookEvent
        {
            EventType = "charge.refunded",
            BookingId = setup.bookingId,
            RefundId = $"ref_{Guid.CreateVersion7()}",
            Amount = 979.0m
        };

        await MessagePublisher.PublishAsync("stripe-events", refundWebhookEvent);

        // Wait briefly for consumer processing
        await Task.Delay(2000);

        // Assert: Only 1 credit note exists for this booking
        DbContext.ChangeTracker.Clear();
        int creditNoteCount = await DbContext.Set<Invoice>()
            .AsNoTracking()
            .CountAsync(i => i.BookingId == setup.bookingId && i.InvoiceType == InvoiceType.CreditNote);

        creditNoteCount.Should().Be(1);
    }

    [Fact]
    public async Task CreditNoteGeneration_ShouldNotGenerateCreditNote_WhenNoOriginalInvoiceExists()
    {
        // Arrange: Setup a booking in RefundProcessing
        var setupResult = await BookingTestHelpers.SetupRefundProcessingBookingAsync(this);
        Guid bookingId = setupResult.bookingId;

        // Remove any invoice created during setup to simulate a booking without an existing invoice
        await DbContext.Set<Invoice>()
            .Where(i => i.BookingId == bookingId)
            .ExecuteDeleteAsync();
        DbContext.ChangeTracker.Clear();

        // Act: Publish charge.refunded
        var refundWebhookEvent = new StripeWebhookEvent
        {
            EventType = "charge.refunded",
            BookingId = bookingId,
            RefundId = $"ref_{Guid.CreateVersion7()}",
            Amount = 100.0m
        };

        await MessagePublisher.PublishAsync("stripe-events", refundWebhookEvent);

        // Wait briefly for consumer to complete
        await Task.Delay(2000);

        // Assert: No credit note was created because original invoice did not exist
        DbContext.ChangeTracker.Clear();
        Invoice? creditNote = await DbContext.Set<Invoice>()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.BookingId == bookingId && i.InvoiceType == InvoiceType.CreditNote);

        creditNote.Should().BeNull();
    }
}
