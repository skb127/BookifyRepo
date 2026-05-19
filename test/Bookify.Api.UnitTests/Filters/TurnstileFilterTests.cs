using Bookify.Api.Filters.Turnstile;
using Bookify.Application.Abstractions.Security;
using Bookify.Domain.Abstractions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using NSubstitute;

namespace Bookify.Api.UnitTests.Filters;

public class TurnstileFilterTests
{
    private readonly ITurnstileValidator _turnstileValidatorMock;
    private readonly TurnstileFilter _filter;

    public TurnstileFilterTests()
    {
        _turnstileValidatorMock = Substitute.For<ITurnstileValidator>();
        _filter = new TurnstileFilter(_turnstileValidatorMock);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldReturnBadRequest_WhenHeaderIsMissing()
    {
        // Arrange
        ActionExecutingContext context = BuildContext(tokenHeaderValue: null);
        bool nextCalled = false;

        // Act
        await _filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        // Assert
        nextCalled.Should().BeFalse();
        context.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().Be("Missing Turnstile token");
        
        await _turnstileValidatorMock.DidNotReceiveWithAnyArgs()
            .Validate(default!, default);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldReturnBadRequest_WhenTokenIsEmpty()
    {
        // Arrange
        ActionExecutingContext context = BuildContext(tokenHeaderValue: "   ");
        bool nextCalled = false;

        // Act
        await _filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        // Assert
        nextCalled.Should().BeFalse();
        context.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().Be("Empty Turnstile token");
            
        await _turnstileValidatorMock.DidNotReceiveWithAnyArgs()
            .Validate(default!, default);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldReturnBadRequest_WhenValidatorReturnsFailure()
    {
        // Arrange
        const string token = "invalid-token";
        ActionExecutingContext context = BuildContext(tokenHeaderValue: token);
        bool nextCalled = false;

        _turnstileValidatorMock.Validate(token, Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new Error("Turnstile.Invalid", "Invalid token")));

        // Act
        await _filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        // Assert
        nextCalled.Should().BeFalse();
        context.Result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().Be("Invalid Turnstile token");
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldCallNext_WhenValidatorReturnsSuccess()
    {
        // Arrange
        const string token = "valid-token";
        ActionExecutingContext context = BuildContext(tokenHeaderValue: token);
        bool nextCalled = false;

        _turnstileValidatorMock.Validate(token, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        await _filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        // Assert
        nextCalled.Should().BeTrue();
        context.Result.Should().BeNull(); // Filter did not short-circuit
    }

    private static ActionExecutingContext BuildContext(string? tokenHeaderValue)
    {
        var httpContext = new DefaultHttpContext();
        if (tokenHeaderValue is not null)
        {
            httpContext.Request.Headers.Append("X-Turnstile-Token", tokenHeaderValue);
        }

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new ActionExecutingContext(
            actionContext,
            filters: [],
            actionArguments: new Dictionary<string, object?>(),
            controller: new object());
    }
}
