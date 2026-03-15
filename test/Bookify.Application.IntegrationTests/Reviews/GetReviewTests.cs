using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.IntegrationTests.Bookings;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.Reviews.GetReview;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Reviews;

[Collection("IntegrationTests")]
public class GetReviewTests : BaseIntegrationTest
{
    public GetReviewTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetReview_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/reviews/{Guid.CreateVersion7()}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetReview_ShouldReturn404_WhenReviewDoesNotExist()
    {
        // Arrange
        var (_, _, _, guestToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/reviews/{Guid.CreateVersion7()}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Review.NotFound");
    }

    [Fact]
    public async Task GetReview_ShouldReturn200_WhenReviewExists()
    {
        // Arrange
        var reviewsToCreate = new List<(int Rating, string Comment)>
        {
            (5, "Perfect stay!")
        };

        var (apartmentId, bookingIds, _, _, guestToken, _) = await BookingTestHelpers.SetupApartmentWithMultipleReviewedBookingsAsync(this, reviewsToCreate);
        var bookingId = bookingIds[0];
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act 1: Get Reviews for Apartment to grab the Review ID
        HttpResponseMessage apartmentReviewsResponse = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{apartmentId}/reviews", UriKind.Relative));

        apartmentReviewsResponse.EnsureSuccessStatusCode();

        var apartmentReviewsBody = await apartmentReviewsResponse.Content.ReadFromJsonAsync<Application.Reviews.GetApartmentReviews.ApartmentReviewsResponse>();

        Guid reviewId = apartmentReviewsBody!.Items[0].Id;

        // Act 2: Get the specific Review by ID
        HttpResponseMessage response = await HttpClient.GetAsync(
            new Uri($"api/v1/reviews/{reviewId}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var reviewResponse = await response.Content.ReadFromJsonAsync<ReviewResponse>();

        reviewResponse.Should().NotBeNull();
        reviewResponse.Id.Should().Be(reviewId);
        reviewResponse.BookingId.Should().Be(bookingId);
        reviewResponse.ApartmentId.Should().Be(apartmentId);
        reviewResponse.Rating.Should().Be(5);
        reviewResponse.Comment.Should().Be("Perfect stay!");
    }
}
