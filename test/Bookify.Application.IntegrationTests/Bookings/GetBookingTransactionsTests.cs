using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.Bookings.GetBookingTransactions;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Bookings;

public class GetBookingTransactionsTests : BaseIntegrationTest
{
    public GetBookingTransactionsTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetBookingTransactions_ShouldReturnForbidden_WhenUserIsNotAdmin()
    {
        // Arrange - guest user
        var (_, _, bookingId, guestToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act
        HttpResponseMessage response =
            await HttpClient.GetAsync(new Uri($"api/v1/bookings/{bookingId}/transactions", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetBookingTransactions_ShouldReturnNotFound_WhenBookingDoesNotExist()
    {
        // Arrange - admin user
        string adminToken = await GetAdminTokenAsync();
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, adminToken);
        Guid nonExistentBookingId = Guid.NewGuid();

        // Act
        HttpResponseMessage response =
            await HttpClient.GetAsync(new Uri($"api/v1/bookings/{nonExistentBookingId}/transactions",
                UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetBookingTransactions_ShouldReturnOk_WhenBookingExistsAndUserIsAdmin()
    {
        // Arrange
        var (_, _, bookingId, _, _) = await BookingTestHelpers.SetupConfirmedPaidBookingAsync(this);
        string adminToken = await GetAdminTokenAsync();
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, adminToken);

        // Act
        HttpResponseMessage response =
            await HttpClient.GetAsync(new Uri($"api/v1/bookings/{bookingId}/transactions", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var transactions = await response.Content.ReadFromJsonAsync<List<BookingTransactionResponse>>();
        transactions.Should().NotBeNull().And.NotBeEmpty();
        transactions[0].BookingId.Should().Be(bookingId);
        transactions[0].ProviderStatus.Should().Be("paid");
    }
}
