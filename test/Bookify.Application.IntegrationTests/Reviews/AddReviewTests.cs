using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Reviews;
using Bookify.Application.IntegrationTests.Bookings;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Reviews;

[Collection("IntegrationTests")]
public class AddReviewTests : BaseIntegrationTest
{
    public AddReviewTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task AddReview_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;
        var request = new AddReviewRequest(Guid.CreateVersion7(), 5, "Great!")
        {
            BookingId = Guid.CreateVersion7(),
            Rating = 5,
            Comment = "Great!"
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/reviews", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddReview_ShouldReturn400_WhenBookingIsNotCompleted()
    {
        // Arrange
        var (_, _, bookingId, guestToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var request = new AddReviewRequest(bookingId, 5, "Great!")
        {
            BookingId = bookingId,
            Rating = 5,
            Comment = "Great!"
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/reviews", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Review.NotEligible");
    }

    [Fact]
    public async Task AddReview_ShouldReturn400_WhenAlreadyReviewed()
    {
        // Arrange
        var reviewsToCreate = new List<(int Rating, string Comment)>
        {
            (5, "Absolutely loved it")
        };

        var (_, bookingIds, _, _, guestToken, _) = await BookingTestHelpers.SetupApartmentWithMultipleReviewedBookingsAsync(this, reviewsToCreate);
        var bookingId = bookingIds[0];
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var request = new AddReviewRequest(bookingId, 4, "Trying to review again")
        {
            BookingId = bookingId,
            Rating = 4,
            Comment = "Trying to review again"
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/reviews", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Review.AlreadyReviewed");
    }

    [Fact]
    public async Task AddReview_ShouldReturn201_WhenReviewIsValid()
    {
        // Arrange
        var (apartmentId, bookingId, _, _, guestToken, _) = await BookingTestHelpers.SetupCompletedBookingAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var request = new AddReviewRequest(bookingId, 5, "Perfect stay!")
        {
            BookingId = bookingId,
            Rating = 5,
            Comment = "Perfect stay!"
        };

        // Act 1: Create Review
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/v1/reviews", request);

        // Assert 1: Created properly
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        Guid reviewId = await response.Content.ReadFromJsonAsync<Guid>();
        reviewId.Should().NotBeEmpty();

        // Act 2: Fetch the created Review
        HttpResponseMessage getResponse = await HttpClient.GetAsync(
            new Uri($"api/v1/reviews/{reviewId}", UriKind.Relative));

        // Assert 2: Validate the Review details
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reviewResponse = await getResponse.Content.ReadFromJsonAsync<Application.Reviews.GetReview.ReviewResponse>();

        reviewResponse.Should().NotBeNull();
        reviewResponse.Id.Should().Be(reviewId);
        reviewResponse.BookingId.Should().Be(bookingId);
        reviewResponse.ApartmentId.Should().Be(apartmentId);
        reviewResponse.Rating.Should().Be(5);
        reviewResponse.Comment.Should().Be("Perfect stay!");
    }
}
