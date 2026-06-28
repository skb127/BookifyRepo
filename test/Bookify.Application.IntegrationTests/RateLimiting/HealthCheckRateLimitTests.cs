#pragma warning disable
using System.Net;
using System.Net.Http.Json;
using Bookify.Application.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bookify.Application.IntegrationTests.RateLimiting;

public class HealthCheckRateLimitTests : RateLimitIntegrationTest
{
    public HealthCheckRateLimitTests(RateLimitTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnTooManyRequests_WhenLimitExceeded()
    {
        // Limit is 2 req per 10s per IP
        // First 2 requests should be accepted
        for (int i = 0; i < 2; i++)
        {
            var response = await HttpClient.GetAsync("/health");
            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        // 3rd request should fail with 429
        var exceededResponse = await HttpClient.GetAsync("/health");
        exceededResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var problemDetails = await exceededResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails.Status.Should().Be(StatusCodes.Status429TooManyRequests);
        problemDetails.Title.Should().Be("Too Many Requests");

        // Wait for window to pass, adding a small margin (1s) to avoid race conditions with the time window
        await Task.Delay(16000);

        // Should be able to request again
        var resetResponse = await HttpClient.GetAsync("/health");
        resetResponse.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }
}
