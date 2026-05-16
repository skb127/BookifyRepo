#pragma warning disable
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bookify.Application.IntegrationTests.RateLimiting;

public class GlobalRateLimitTests : RateLimitIntegrationTest
{
    public GlobalRateLimitTests(RateLimitTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GlobalLimit_ShouldReturnTooManyRequests_WhenLimitExceeded_ForAuthenticatedUser()
    {
        // Limit is 3 req per 10s globally
        string accessToken = await GetAccessTokenAsync(RateLimitUserData.GlobalLimiterUserA.Email, RateLimitUserData.GlobalLimiterUserA.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Send 3 requests (should be OK) - targeting a GET endpoint without specific policy
        for (int i = 0; i < 3; i++)
        {
            var response = await HttpClient.GetAsync("api/v1/users/me");
            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        // 4th request should fail
        var exceededResponse = await HttpClient.GetAsync("api/v1/users/me");
        exceededResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        
        var problemDetails = await exceededResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Status.Should().Be(StatusCodes.Status429TooManyRequests);
        
        // Ensure another user is not affected
        string accessTokenB = await GetAccessTokenAsync(RateLimitUserData.GlobalLimiterUserB.Email, RateLimitUserData.GlobalLimiterUserB.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessTokenB);
        
        var responseUserB = await HttpClient.GetAsync("api/v1/users/me");
        responseUserB.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }
}
