using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Reviews;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.IntegrationTests.Bookings;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.IntegrationTests.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Reviews;

[Collection("IntegrationTests")]
public class UpdateReviewTests : BaseIntegrationTest
{
    private readonly MockEmailService _mockEmailService;

    public UpdateReviewTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _mockEmailService = factory.MockEmailService;
    }

    [Fact]
    public async Task UpdateReview_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;
        var request = new UpdateReviewRequest(5, "Updated comment");

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"api/v1/reviews/{Guid.CreateVersion7()}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateReview_ShouldReturn404_WhenReviewDoesNotExist()
    {
        // Arrange
        string guestToken = await GetAccessToken(UserData.UpdateReviewSecondaryUserRequest.Email, UserData.UpdateReviewSecondaryUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        var request = new UpdateReviewRequest(5, "Updated comment");

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"api/v1/reviews/{Guid.CreateVersion7()}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Review.NotFound");
    }

    [Fact]
    public async Task UpdateReview_ShouldReturn403_WhenCallerIsNotAuthor()
    {
        // Arrange
        // Setup a booking with a review by User A (guest)
        var reviewsToCreate = new List<(int Rating, string Comment)>
        {
            (5, "Great stay!")
        };
        var (apartmentId, _, _, _, _, _) = await BookingTestHelpers.SetupApartmentWithMultipleReviewedBookingsAsync(this, reviewsToCreate);

        // Get the review ID
        HttpResponseMessage apartmentReviewsResponse = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{apartmentId}/reviews", UriKind.Relative));
        var apartmentReviewsBody = await apartmentReviewsResponse.Content.ReadFromJsonAsync<Application.Reviews.GetApartmentReviews.ApartmentReviewsResponse>();
        Guid reviewId = apartmentReviewsBody!.Items[0].Id;

        // Create a different user (User B) and set their token
        string guest2Token = await GetAccessToken(UserData.UpdateReviewSecondaryUserRequest.Email, UserData.UpdateReviewSecondaryUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guest2Token);

        var request = new UpdateReviewRequest(4, "I want to change someone else's review");

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"api/v1/reviews/{reviewId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Review.NotAuthor");
    }

    [Fact]
    public async Task UpdateReview_ShouldReturn400_WhenRatingIsInvalid()
    {
        // Arrange
        var reviewsToCreate = new List<(int Rating, string Comment)>
        {
            (5, "Perfect stay!")
        };
        var (apartmentId, _, _, _, guestToken, _) = await BookingTestHelpers.SetupApartmentWithMultipleReviewedBookingsAsync(this, reviewsToCreate);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Get review ID
        HttpResponseMessage apartmentReviewsResponse = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{apartmentId}/reviews", UriKind.Relative));
        var apartmentReviewsBody = await apartmentReviewsResponse.Content.ReadFromJsonAsync<Application.Reviews.GetApartmentReviews.ApartmentReviewsResponse>();
        Guid reviewId = apartmentReviewsBody!.Items[0].Id;

        // Rating out of 1-5 range
        var request = new UpdateReviewRequest(6, "Updated comment");

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"api/v1/reviews/{reviewId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateReview_ShouldReturn400_WhenCommentIsTooLong()
    {
        // Arrange
        var reviewsToCreate = new List<(int Rating, string Comment)>
        {
            (5, "Perfect stay!")
        };
        var (apartmentId, _, _, _, guestToken, _) = await BookingTestHelpers.SetupApartmentWithMultipleReviewedBookingsAsync(this, reviewsToCreate);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Get review ID
        HttpResponseMessage apartmentReviewsResponse = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{apartmentId}/reviews", UriKind.Relative));
        var apartmentReviewsBody = await apartmentReviewsResponse.Content.ReadFromJsonAsync<Application.Reviews.GetApartmentReviews.ApartmentReviewsResponse>();
        Guid reviewId = apartmentReviewsBody!.Items[0].Id;

        // Comment length out of bounds
        var request = new UpdateReviewRequest(5, new string('A', 201));

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"api/v1/reviews/{reviewId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateReview_ShouldReturn204_WhenUpdateIsValid()
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

        var request = new UpdateReviewRequest(3, "Actually, just OK.");

        DateTime testStartTime = DateTime.UtcNow;

        // Act 2: Update Review
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"api/v1/reviews/{reviewId}", request);

        // Assert 1: Updated properly
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act 3: Fetch the created Review
        HttpResponseMessage getResponse = await HttpClient.GetAsync(
            new Uri($"api/v1/reviews/{reviewId}", UriKind.Relative));

        // Assert 2: Validate the Review details have changed
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var reviewResponse = await getResponse.Content.ReadFromJsonAsync<Application.Reviews.GetReview.ReviewResponse>();

        reviewResponse.Should().NotBeNull();
        reviewResponse.Id.Should().Be(reviewId);
        reviewResponse.BookingId.Should().Be(bookingId);
        reviewResponse.ApartmentId.Should().Be(apartmentId);

        // Assert updated values
        reviewResponse.Rating.Should().Be(3);
        reviewResponse.Comment.Should().Be("Actually, just OK.");

        // Assert 3: Verify Email was sent by Outbox
        EmailMessage email = await _mockEmailService.WaitForEmailToAsync(
            recipientEmail: "host@bookify.com",
            subject: "A review for your apartment has been updated",
            timeoutMs: 10_000,
            since: testStartTime);

        email.Should().NotBeNull();
        email.Body.Should().Contain("The new rating is <strong>3</strong>.");
    }
}
