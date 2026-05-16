using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.IntegrationTests.Bookings;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.IntegrationTests.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Reviews;

public class DeleteReviewTests : BaseIntegrationTest
{
    private const string Password = "Password123!";

    public DeleteReviewTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task DeleteReview_ShouldReturn401_WhenNotAuthenticated()
    {
        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync(
            new Uri($"api/v1/reviews/{Guid.CreateVersion7()}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteReview_ShouldReturn404_WhenReviewDoesNotExist()
    {
        // Arrange

        string accessToken = await GetAccessToken(UserData.DeleteReviewSecondaryUserRequest.Email, UserData.DeleteReviewSecondaryUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync(
            new Uri($"api/v1/reviews/{Guid.CreateVersion7()}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Review.NotFound"); // Expecting ProblemDetails with this error code
    }

    [Fact]
    public async Task DeleteReview_ShouldReturn403_WhenCallerIsNotAuthorNorAdmin()
    {
        // Arrange
        // Create an apartment with a completed booking and a review (Review is created by 'guestEmail')
        var reviewsToCreate = new List<(int Rating, string Comment)> { (5, "Great place!") };
        var (apartmentId, _, _, _, guestToken, _) =
            await BookingTestHelpers.SetupApartmentWithMultipleReviewedBookingsAsync(this, reviewsToCreate, Password);

        // Fetch the review ID assigned to this apartment via the guest token
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        HttpResponseMessage getApartmentReviewsResponse = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{apartmentId}/reviews", UriKind.Relative));
        var reviewList = await getApartmentReviewsResponse.Content.ReadFromJsonAsync<Bookify.Application.Reviews.GetApartmentReviews.ApartmentReviewsResponse>();
        Guid reviewId = reviewList!.Items[0].Id;

        string secondaryAccessToken = await GetAccessToken(UserData.DeleteReviewTertiaryUserRequest.Email, UserData.DeleteReviewTertiaryUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            secondaryAccessToken);

        // Act - Attemting to delete someone else's review
        HttpResponseMessage response = await HttpClient.DeleteAsync(
            new Uri($"api/v1/reviews/{reviewId}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Review.NotAuthor");
    }

    [Fact]
    public async Task DeleteReview_ShouldReturn204_WhenCallerIsAuthor()
    {
        // Arrange
        var reviewsToCreate = new List<(int Rating, string Comment)> { (5, "Great place!") };
        var (apartmentId, _, _, _, guestToken, _) =
            await BookingTestHelpers.SetupApartmentWithMultipleReviewedBookingsAsync(this, reviewsToCreate, Password);

        // Fetch the review ID
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        HttpResponseMessage getApartmentReviewsResponse = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{apartmentId}/reviews", UriKind.Relative));
        var reviewList = await getApartmentReviewsResponse.Content.ReadFromJsonAsync<Bookify.Application.Reviews.GetApartmentReviews.ApartmentReviewsResponse>();
        Guid reviewId = reviewList!.Items[0].Id;

        // Act - Author deletes their own review
        HttpResponseMessage response = await HttpClient.DeleteAsync(
            new Uri($"api/v1/reviews/{reviewId}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify review is no longer accessible
        HttpResponseMessage getReviewResponse = await HttpClient.GetAsync(
            new Uri($"api/v1/reviews/{reviewId}", UriKind.Relative));
        getReviewResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteReview_ShouldReturn204_WhenCallerIsAdmin()
    {
        // Arrange
        var reviewsToCreate = new List<(int Rating, string Comment)> { (5, "Great place!") };
        var (apartmentId, _, ownerToken, ownerEmail, guestToken, _) =
            await BookingTestHelpers.SetupApartmentWithMultipleReviewedBookingsAsync(this, reviewsToCreate, Password);

        // Fetch the review ID (using guest token)
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        HttpResponseMessage getApartmentReviewsResponse = await HttpClient.GetAsync(
            new Uri($"api/v1/apartments/{apartmentId}/reviews", UriKind.Relative));
        var reviewList = await getApartmentReviewsResponse.Content.ReadFromJsonAsync<Bookify.Application.Reviews.GetApartmentReviews.ApartmentReviewsResponse>();
        Guid reviewId = reviewList!.Items[0].Id;

        // Promote the owner to Admin (AdminOwner) so they have Moderation capabilities
        await PromoteToAdminAsync(ownerEmail);

        // Authenticate as Admin (the owner)
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, ownerToken);

        // Act - Admin deletes someone's review
        HttpResponseMessage response = await HttpClient.DeleteAsync(
            new Uri($"api/v1/reviews/{reviewId}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify review is no longer accessible
        HttpResponseMessage getReviewResponse = await HttpClient.GetAsync(
            new Uri($"api/v1/reviews/{reviewId}", UriKind.Relative));
        getReviewResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
