using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bookify.Application.IntegrationTests.RateLimiting;

public class SearchRateLimitTests : RateLimitIntegrationTest
{
    public SearchRateLimitTests(RateLimitTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Search_ShouldReturnTooManyRequests_WhenLimitExceeded_ForAuthenticatedUser()
    {
        // Limit is 2 req per 10s per user
        string accessToken =
            await GetAccessTokenAsync(RateLimitUserData.SearchUser.Email, RateLimitUserData.SearchUser.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Send 2 requests (should be OK)
        for (int i = 0; i < 2; i++)
        {
            var response =
                await HttpClient.GetAsync(new Uri("api/v1/apartments?startDate=2024-01-01&endDate=2024-01-10",
                    UriKind.Relative));
            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        // 3rd request should fail
        var exceededResponse =
            await HttpClient.GetAsync(new Uri("api/v1/apartments?startDate=2024-01-01&endDate=2024-01-10",
                UriKind.Relative));
        exceededResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var problemDetails = await exceededResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Status.Should().Be(StatusCodes.Status429TooManyRequests);
    }
}
