﻿﻿﻿using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.Bookings.GetBooking;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Bookings;

public class GetBookingTests : BaseIntegrationTest
{
    private static readonly Guid BookingId = Guid.CreateVersion7();

    public GetBookingTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetBooking_ShouldReturnNotFound_WhenBookingIsNotFound()
    {
        // Arrange
        // Create a test user to get a valid token
        var (_, _, _, guestToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri($"api/v1/bookings/{BookingId}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetBooking_ShouldReturnOk_WhenBookingExists()
    {
        // Arrange
        var (_, _, bookingId, guestToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);
        
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri($"api/v1/bookings/{bookingId}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var bookingResponse = await response.Content.ReadFromJsonAsync<BookingResponse>();
        bookingResponse.Should().NotBeNull();
        bookingResponse.Id.Should().Be(bookingId);
    }
}
