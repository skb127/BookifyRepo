using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.IntegrationTests.Bookings;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.Reviews.GetApartmentReviews;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Apartments;

[Collection("IntegrationTests")]
public class GetApartmentReviewsTests : BaseIntegrationTest
{
    public GetApartmentReviewsTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetApartmentReviews_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{Guid.CreateVersion7()}/reviews", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetApartmentReviews_ShouldReturn404_WhenApartmentDoesNotExist()
    {
        // Arrange
        var (_, _, _, _, guestToken, _) = await BookingTestHelpers.SetupApartmentWithOwnerAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{Guid.CreateVersion7()}/reviews", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Apartment.NotFound");
    }

    [Fact]
    public async Task GetApartmentReviews_ShouldReturn400_WhenPageIsInvalid()
    {
        // Arrange
        var (apartmentId, _, _, _, guestToken, _) = await BookingTestHelpers.SetupApartmentWithOwnerAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{apartmentId}/reviews?page=0&pageSize=10", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetApartmentReviews_ShouldReturn200_WithEmptyList_WhenApartmentHasNoReviews()
    {
        // Arrange
        var (apartmentId, _, _, _, guestToken, _) = await BookingTestHelpers.SetupApartmentWithOwnerAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{apartmentId}/reviews", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseBody = await response.Content.ReadFromJsonAsync<ApartmentReviewsResponse>();

        responseBody.Should().NotBeNull();
        responseBody.Items.Should().BeEmpty();
        responseBody.TotalCount.Should().Be(0);
        responseBody.AverageRating.Should().Be(0.0);
        responseBody.HasNextPage.Should().BeFalse();
        responseBody.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public async Task GetApartmentReviews_ShouldReturn200_WithCorrectAverageRating()
    {
        // Arrange
        var reviewsToCreate = new List<(int Rating, string Comment)>
        {
            (4, "Great place!"),
            (5, "Absolutely loved it")
        };

        var (apartmentId, _, _, _, guestToken, _) = await BookingTestHelpers.SetupCompletedBookingWithReviewsAsync(this, reviewsToCreate);

        // Act
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{apartmentId}/reviews", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseBody = await response.Content.ReadFromJsonAsync<ApartmentReviewsResponse>();

        responseBody.Should().NotBeNull();
        responseBody.Items.Should().HaveCount(2);
        responseBody.TotalCount.Should().Be(2);

        // The average of 4 and 5 is 4.5
        responseBody.AverageRating.Should().Be(4.5);

        responseBody.HasNextPage.Should().BeFalse(); // default page size is 10, total 2
        responseBody.HasPreviousPage.Should().BeFalse(); // default page is 1

        // Ensure items contain our inserted values
        responseBody.Items.Should().Contain(r => r.Rating == 4 && r.Comment == "Great place!");
        responseBody.Items.Should().Contain(r => r.Rating == 5 && r.Comment == "Absolutely loved it");
    }
}
