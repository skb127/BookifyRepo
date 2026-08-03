#pragma warning disable
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Users.Requests;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bookify.Application.IntegrationTests.RateLimiting;

public class WriteOperationsRateLimitTests : RateLimitIntegrationTest
{
    public WriteOperationsRateLimitTests(RateLimitTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task WriteOperations_ShouldReturnTooManyRequests_WhenLimitExceeded_ForAnonymousUser()
    {
        // Limit is 2 req per 10s per IP
        // First 2 requests should be accepted (even if auth fails)
        for (int i = 0; i < 2; i++)
        {
            var request = new LoginUserRequest("invalid@test.com", "Invalid123!");
            var response = await HttpClient.PostAsJsonAsync("api/v1/users/login", request);
            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        // 3rd request should fail with 429
        var request3 = new LoginUserRequest("invalid@test.com", "Invalid123!");
        var exceededResponse = await HttpClient.PostAsJsonAsync("api/v1/users/login", request3);
        exceededResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        
        var problemDetails = await exceededResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Status.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task WriteOperations_ShouldReturnTooManyRequests_WhenLimitExceeded_ForAuthenticatedUser()
    {
        // Limit is 2 req per 10s per User
        string accessToken = await GetAccessTokenAsync(RateLimitUserData.WriteOpsUser.Email, RateLimitUserData.WriteOpsUser.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var updateRequest = new UpdateUserProfileRequest(
            RateLimitUserData.WriteOpsUser.FirstName,
            RateLimitUserData.WriteOpsUser.LastName,
            "123456789",
            (DateOnly)RateLimitUserData.WriteOpsUser.DateOfBirth,
            RateLimitUserData.WriteOpsUser.Password);

        // First 2 requests should be accepted
        for (int i = 0; i < 2; i++)
        {
            var response = await HttpClient.PutAsJsonAsync("api/v1/users/profile", updateRequest);
            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        // 3rd request should fail
        var exceededResponse = await HttpClient.PutAsJsonAsync("api/v1/users/profile", updateRequest);
        exceededResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        
        var problemDetails = await exceededResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Status.Should().Be(StatusCodes.Status429TooManyRequests);
    }
}
