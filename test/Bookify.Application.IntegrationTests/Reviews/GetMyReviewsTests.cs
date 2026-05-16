using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.Common;
using Bookify.Application.IntegrationTests.Bookings;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.Reviews.GetMyReviews;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Reviews;

public class GetMyReviewsTests : BaseIntegrationTest
{
    public GetMyReviewsTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetMyReviews_ShouldReturn401_WhenNotAuthenticated()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/reviews/me", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyReviews_ShouldReturn200_WithEmptyList_WhenNoReviews()
    {
        // Arrange
        var (_, _, _, guestToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/reviews/me", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<MyReviewResponse>>();
        
        body.Should().NotBeNull();
        body.Items.Should().BeEmpty();
        body.TotalCount.Should().Be(0);
        body.Page.Should().Be(1);
        body.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetMyReviews_ShouldReturn200_WithCorrectItems_WhenReviewsExist()
    {
        // Arrange
        var reviewsToCreate = new List<(int Rating, string Comment)>
        {
            (5, "Perfect stay!"),
            (4, "Very nice, but a bit noisy in the morning.")
        };

        var (apartmentId, bookingIds, _, _, guestToken, _) = await BookingTestHelpers.SetupApartmentWithMultipleReviewedBookingsAsync(this, reviewsToCreate);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/reviews/me", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResponse<MyReviewResponse>>();

        body.Should().NotBeNull();
        body.TotalCount.Should().Be(2);
        body.Items.Should().HaveCount(2);

        body.Items.Should().Contain(r => r.Rating == 5 && r.Comment == "Perfect stay!");
        body.Items.Should().Contain(r => r.Rating == 4 && r.Comment == "Very nice, but a bit noisy in the morning.");
        
        foreach(var item in body.Items) 
        {
            item.Id.Should().NotBeEmpty();
            bookingIds.Should().Contain(item.BookingId);
            item.ApartmentId.Should().Be(apartmentId);
        }
    }

    [Fact]
    public async Task GetMyReviews_ShouldReturn400_WhenPageIsZero()
    {
        // Arrange
        var (_, _, _, guestToken, _) = await BookingTestHelpers.SetupReservedBookingAsync(this);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/reviews/me?page=0", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
