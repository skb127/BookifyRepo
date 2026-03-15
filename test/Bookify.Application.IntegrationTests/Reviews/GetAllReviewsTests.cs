using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.IntegrationTests.Bookings;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.IntegrationTests.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Reviews;

[Collection("IntegrationTests")]
public class GetAllReviewsTests : BaseIntegrationTest
{
    private const string Password = "Password123!";

    public GetAllReviewsTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetAllReviews_ShouldReturn401_WhenNotAuthenticated()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/reviews", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAllReviews_ShouldReturn403_WhenCallerIsRegularUser()
    {
        // Arrange
        // We use GetAllReviewsRegularUserRequest but we DO NOT promote it to Admin
        string accessToken = await GetAccessToken(UserData.GetAllReviewsRegularUserRequest.Email, UserData.GetAllReviewsRegularUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/reviews", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAllReviews_ShouldReturn400_WhenPageSizeIsInvalid()
    {
        // Arrange
        await PromoteToAdminAsync(UserData.GetAllReviewsAdminUserRequest.Email);
        string accessToken = await GetAccessToken(UserData.GetAllReviewsAdminUserRequest.Email, UserData.GetAllReviewsAdminUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/reviews?page=1&pageSize=0", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("PageSize");

    }

    [Fact]
    public async Task GetAllReviews_ShouldReturn200_WithEmptyList_WhenNoReviewsExist()
    {
        // Arrange
        await PromoteToAdminAsync(UserData.GetAllReviewsAdminUserRequest.Email);
        string accessToken = await GetAccessToken(UserData.GetAllReviewsAdminUserRequest.Email, UserData.GetAllReviewsAdminUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        Guid nonExistentApartmentId = Guid.CreateVersion7();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri($"api/v1/reviews?apartmentId={nonExistentApartmentId}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Common.PagedResponse<Bookify.Application.Reviews.GetAllReviews.AllReviewsResponse>>();


        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAllReviews_ShouldReturn200_WithList_WhenReviewsExist()
    {
        // Arrange
        var reviewsToCreate = new List<(int Rating, string Comment)> { (5, "Great place!") };
        await BookingTestHelpers.SetupApartmentWithMultipleReviewedBookingsAsync(this, reviewsToCreate, Password);

        // Promote the admin user so they can read all reviews
        await PromoteToAdminAsync(UserData.GetAllReviewsAdminUserRequest.Email);
        string accessToken = await GetAccessToken(UserData.GetAllReviewsAdminUserRequest.Email, UserData.GetAllReviewsAdminUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/reviews", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Common.PagedResponse<Bookify.Application.Reviews.GetAllReviews.AllReviewsResponse>>();

        result.Should().NotBeNull();
        result.Items.Should().NotBeEmpty();
        result.TotalCount.Should().BeGreaterThan(0);

        var review = result.Items[0];
        review.Id.Should().NotBeEmpty();
        review.BookingId.Should().NotBeEmpty();
        review.ApartmentId.Should().NotBeEmpty();
        review.UserId.Should().NotBeEmpty();
        review.Rating.Should().Be(5);
        review.Comment.Should().Be("Great place!");
    }

    [Fact]
    public async Task GetAllReviews_ShouldReturn200_FilteredByApartmentId()
    {
        // Arrange
        var reviewsToCreate = new List<(int Rating, string Comment)> { (4, "Nice place!") };
        var (apartmentId, _, _, _, _, _) =
            await BookingTestHelpers.SetupApartmentWithMultipleReviewedBookingsAsync(this, reviewsToCreate, Password);

        // We run a second setup to ensure there are other reviews in the system
        await BookingTestHelpers.SetupApartmentWithMultipleReviewedBookingsAsync(this, reviewsToCreate, Password);

        await PromoteToAdminAsync(UserData.GetAllReviewsAdminUserRequest.Email);
        string accessToken = await GetAccessToken(UserData.GetAllReviewsAdminUserRequest.Email, UserData.GetAllReviewsAdminUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri($"api/v1/reviews?apartmentId={apartmentId}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Common.PagedResponse<Bookify.Application.Reviews.GetAllReviews.AllReviewsResponse>>();


        result.Should().NotBeNull();
        result.Items.Should().NotBeEmpty();

        // All returned reviews should belong to the requested apartment
        foreach (var review in result.Items)
        {
            review.ApartmentId.Should().Be(apartmentId);
        }
    }

    [Fact]
    public async Task GetAllReviews_ShouldReturn200_FilteredByUserId()
    {
        // Arrange
        var reviewsToCreate = new List<(int Rating, string Comment)> { (5, "Super host!") };
        var (_, _, _, _, guestToken, _) =
            await BookingTestHelpers.SetupApartmentWithMultipleReviewedBookingsAsync(this, reviewsToCreate, Password);

        // Resolve the guest's userId by calling GET api/v1/users/me with the guest's token
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);
        var meResponse = await HttpClient.GetAsync(new Uri("api/v1/users/me", UriKind.Relative));
        meResponse.EnsureSuccessStatusCode();
        var me = await meResponse.Content.ReadFromJsonAsync<Bookify.Application.Users.GetLoggedInUser.UserResponse>();
        Guid guestId = me!.Id;

        // We run a second setup to ensure there are other reviews in the system
        await BookingTestHelpers.SetupApartmentWithMultipleReviewedBookingsAsync(this, new List<(int, string)> { (1, "Bad") }, Password);

        await PromoteToAdminAsync(UserData.GetAllReviewsAdminUserRequest.Email);
        string accessToken = await GetAccessToken(UserData.GetAllReviewsAdminUserRequest.Email, UserData.GetAllReviewsAdminUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri($"api/v1/reviews?userId={guestId}", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Common.PagedResponse<Bookify.Application.Reviews.GetAllReviews.AllReviewsResponse>>();

        result.Should().NotBeNull();
        result.Items.Should().NotBeEmpty();

        // All returned reviews should belong to the requested user
        result.Items.Should().OnlyContain(r => r.UserId == guestId);
        result.Items.Count.Should().Be(1);
    }

    [Fact]
    public async Task GetAllReviews_ShouldReturn200_FilteredByIsEdited()
    {
        // Arrange
        var reviewsToCreate = new List<(int Rating, string Comment)> { (3, "Okay place.") };
        var (_, _, _, _, guestToken, _) =
            await BookingTestHelpers.SetupApartmentWithMultipleReviewedBookingsAsync(this, reviewsToCreate, Password);

        // 1. Get the review ID (using guest token or owner token since it was just created)
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Get guest's own reviews to find the review ID
        HttpResponseMessage getMyReviewsResponse = await HttpClient.GetAsync(
            new Uri("api/v1/reviews/me", UriKind.Relative));
        var myReviewList = await getMyReviewsResponse.Content.ReadFromJsonAsync<Common.PagedResponse<Bookify.Application.Reviews.GetMyReviews.MyReviewResponse>>();
        Guid reviewId = myReviewList!.Items[0].Id;

        // 2. Edit the review
        var updateRequest = new Bookify.Api.Controllers.Reviews.UpdateReviewRequest(4, "Actually, it's pretty nice!");
        await HttpClient.PutAsJsonAsync(new Uri($"api/v1/reviews/{reviewId}", UriKind.Relative), updateRequest);

        // 3. Authenticate as Admin
        await PromoteToAdminAsync(UserData.GetAllReviewsAdminUserRequest.Email);
        string accessToken = await GetAccessToken(UserData.GetAllReviewsAdminUserRequest.Email, UserData.GetAllReviewsAdminUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act - Fetch only edited reviews
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/reviews?isEdited=true", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<Common.PagedResponse<Bookify.Application.Reviews.GetAllReviews.AllReviewsResponse>>();

        result.Should().NotBeNull();
        result.Items.Should().NotBeEmpty();

        // The review we just edited should be in the list
        var editedReview = result.Items[0];
        editedReview.Should().NotBeNull();
        editedReview.Rating.Should().Be(4);
        editedReview.Comment.Should().Be("Actually, it's pretty nice!");
        editedReview.EditedOnUtc.Should().NotBeNull();
    }
}
