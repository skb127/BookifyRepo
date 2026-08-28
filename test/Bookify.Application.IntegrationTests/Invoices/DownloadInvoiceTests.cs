using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.IntegrationTests.Apartments;
using Bookify.Application.IntegrationTests.Bookings;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.Users.RegisterGuest;
using Bookify.Domain.Bookings;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bookify.Application.IntegrationTests.Invoices;

public class DownloadInvoiceTests : BaseIntegrationTest
{
    public DownloadInvoiceTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task DownloadInvoice_ShouldReturn302Redirect_WhenInvoiceIsGeneratedAndUserIsOwner()
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
    public async Task DownloadInvoice_ShouldReturn404NotFound_WhenBookingDoesNotExist()
    {
        // Arrange
        string guestEmail = $"guest_{Guid.CreateVersion7()}@test.com";
        var registerGuestCommand = new RegisterGuestCommand(
            guestEmail, "Guest", "User", "Password123!", new DateOnly(1995, 5, 5));
        await Sender.Send(registerGuestCommand);
        string guestToken = await GetAccessToken(guestEmail, "Password123!");

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        var randomBookingId = Guid.CreateVersion7();
        var randomInvoiceId = Guid.CreateVersion7();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/bookings/{randomBookingId}/invoices/{randomInvoiceId}/download", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Title.Should().Be(BookingErrors.NotFound.Code);
    }

    [Fact]
    public async Task DownloadInvoice_ShouldReturn404NotFound_WhenInvoiceDoesNotExist()
    {
        // Arrange: Booking exists, but random invoiceId is used
        var setup = await BookingTestHelpers.SetupBookingWithGeneratedInvoiceAsync(this);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, setup.guestToken);

        var randomInvoiceId = Guid.CreateVersion7();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/bookings/{setup.bookingId}/invoices/{randomInvoiceId}/download", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Title.Should().Be(InvoiceErrors.NotFound.Code);
    }

    [Fact]
    public async Task DownloadInvoice_ShouldReturn403Forbidden_WhenUserIsNotBookingOwner()
    {
        // Arrange: Booking & Invoice owned by Guest 1
        var setup = await BookingTestHelpers.SetupBookingWithGeneratedInvoiceAsync(this);

        // Create Guest 2
        string otherGuestEmail = $"other_{Guid.CreateVersion7()}@test.com";
        var registerGuestCommand = new RegisterGuestCommand(
            otherGuestEmail, "Other", "User", "Password123!", new DateOnly(1996, 6, 6));
        await Sender.Send(registerGuestCommand);
        string otherGuestToken = await GetAccessToken(otherGuestEmail, "Password123!");

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, otherGuestToken);

        // Act: Guest 2 attempts to download Guest 1's invoice
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/bookings/{setup.bookingId}/invoices/{setup.invoiceId}/download", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Title.Should().Be(BookingErrors.Unauthorized.Code);
    }

    [Fact]
    public async Task DownloadInvoice_ShouldReturn404NotFound_WhenInvoiceIsStillPending()
    {
        // Arrange: Setup test data without completing payment (so no Generated PDF exists)
        var setup = await BookingTestHelpers.SetupInvoiceTestDataAsync(this);

        // Add a Pending Invoice manually to the database
        var pendingInvoice = Invoice.CreateForBooking(
            setup.bookingId,
            1000.0m,
            210.0m,
            "USD",
            DateTime.UtcNow);

        DbContext.Set<Invoice>().Add(pendingInvoice);
        await DbContext.SaveChangesAsync();

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, setup.guestToken);

        // Act: Attempt to download pending invoice
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/bookings/{setup.bookingId}/invoices/{pendingInvoice.Id}/download", UriKind.Relative));

        // Assert: Pending invoices return 404
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Title.Should().Be(InvoiceErrors.NotFound.Code);
    }

    [Fact]
    public async Task DownloadInvoice_ShouldReturn400BadRequest_WhenBookingIdIsEmpty()
    {
        // Arrange
        string guestEmail = $"guest_{Guid.CreateVersion7()}@test.com";
        var registerGuestCommand = new RegisterGuestCommand(
            guestEmail, "Guest", "User", "Password123!", new DateOnly(1995, 5, 5));
        await Sender.Send(registerGuestCommand);
        string guestToken = await GetAccessToken(guestEmail, "Password123!");

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        var emptyBookingId = Guid.Empty;
        var validInvoiceId = Guid.CreateVersion7();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/bookings/{emptyBookingId}/invoices/{validInvoiceId}/download", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DownloadInvoice_ShouldReturn401Unauthorized_WhenNotAuthenticated()
    {
        // Arrange: No Authorization header
        HttpClient.DefaultRequestHeaders.Authorization = null;

        var randomBookingId = Guid.CreateVersion7();
        var randomInvoiceId = Guid.CreateVersion7();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/bookings/{randomBookingId}/invoices/{randomInvoiceId}/download", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
