using System.Text;
using System.Text.Json;
using Bookify.Api.Filters.Idempotency;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Bookify.Api.UnitTests.Filters;

public class IdempotentAttributeTests
{
    private readonly IDistributedCache _cacheMock;
    private readonly IdempotentAttribute _filter;
    private readonly IServiceProvider _serviceProvider;

    public IdempotentAttributeTests()
    {
        _cacheMock = Substitute.For<IDistributedCache>();
        _filter = new IdempotentAttribute();
        
        var services = new ServiceCollection();
        services.AddSingleton(_cacheMock);
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldCallNext_WhenNoIdempotenceKeyHeader()
    {
        // Arrange
        ActionExecutingContext context = BuildContext(null, new { data = "test" });
        bool nextCalled = false;

        // Act
        await _filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        // Assert
        nextCalled.Should().BeTrue();
        await _cacheMock.DidNotReceiveWithAnyArgs().GetStringAsync(default!, default);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldCallNext_WhenIdempotenceKeyIsInvalidGuid()
    {
        // Arrange
        ActionExecutingContext context = BuildContext("invalid-guid", new { data = "test" });
        bool nextCalled = false;

        // Act
        await _filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        // Assert
        nextCalled.Should().BeTrue();
        await _cacheMock.DidNotReceiveWithAnyArgs().GetStringAsync(default!, default);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldReturnCachedResponse_WhenKeyExistsWithSameBodyHash()
    {
        // Arrange
        var key = Guid.NewGuid().ToString();
        var body = new { data = "test" };
        ActionExecutingContext context = BuildContext(key, body);
        bool nextCalled = false;

        string expectedHash = ComputeTestHash(body);
        var cachedResponse = new IdempotentResponse(StatusCodes.Status201Created, new { id = 1 }, expectedHash);
        _cacheMock.GetAsync($"Idempotent_{key}", Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cachedResponse)));

        // Act
        await _filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        // Assert
        nextCalled.Should().BeFalse();
        context.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldReturn422_WhenKeyExistsWithDifferentBodyHash()
    {
        // Arrange
        var key = Guid.NewGuid().ToString();
        var body = new { data = "new-data" };
        ActionExecutingContext context = BuildContext(key, body);
        bool nextCalled = false;

        var cachedResponse = new IdempotentResponse(StatusCodes.Status201Created, new { id = 1 }, "different-hash");
        _cacheMock.GetAsync($"Idempotent_{key}", Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cachedResponse)));

        // Act
        await _filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        // Assert
        nextCalled.Should().BeFalse();
        context.Result.Should().BeOfType<UnprocessableEntityObjectResult>()
            .Which.Value.Should().Be("The Idempotence-Key has already been used with a different request body.");
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldCallNext_AndCacheResponse_WhenKeyIsNew_And2xx()
    {
        // Arrange
        var key = Guid.NewGuid().ToString();
        var body = new { data = "test" };
        ActionExecutingContext context = BuildContext(key, body);
        
        _cacheMock.GetAsync($"Idempotent_{key}", Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);

        var actionExecutedContext = new ActionExecutedContext(
            context,
            [],
            new object())
        {
            Result = new ObjectResult(new { id = 1 }) { StatusCode = StatusCodes.Status201Created }
        };

        // Act
        await _filter.OnActionExecutionAsync(context, () => Task.FromResult(actionExecutedContext));

        // Assert
        await _cacheMock.Received(1).SetAsync(
            $"Idempotent_{key}",
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldNotCacheResponse_WhenKeyIsNew_And4xx()
    {
        // Arrange
        var key = Guid.NewGuid().ToString();
        var body = new { data = "test" };
        ActionExecutingContext context = BuildContext(key, body);
        
        _cacheMock.GetAsync($"Idempotent_{key}", Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);

        var actionExecutedContext = new ActionExecutedContext(
            context,
            [],
            new object())
        {
            Result = new ObjectResult("Error") { StatusCode = StatusCodes.Status400BadRequest }
        };

        // Act
        await _filter.OnActionExecutionAsync(context, () => Task.FromResult(actionExecutedContext));

        // Assert
        await _cacheMock.DidNotReceive().SetAsync(
            Arg.Any<string>(),
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldNotCacheResponse_WhenKeyIsNew_And5xx()
    {
        // Arrange
        var key = Guid.NewGuid().ToString();
        var body = "{\"data\":\"test\"}";
        ActionExecutingContext context = BuildContext(key, body);
        
        _cacheMock.GetAsync($"Idempotent_{key}", Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);

        var actionExecutedContext = new ActionExecutedContext(
            context,
            [],
            new object())
        {
            Result = new ObjectResult("Error") { StatusCode = StatusCodes.Status500InternalServerError }
        };

        // Act
        await _filter.OnActionExecutionAsync(context, () => Task.FromResult(actionExecutedContext));

        // Assert
        await _cacheMock.DidNotReceive().SetAsync(
            Arg.Any<string>(),
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    private ActionExecutingContext BuildContext(string? idempotenceKey, object? argumentValue)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = _serviceProvider
        };

        if (idempotenceKey is not null)
        {
            httpContext.Request.Headers.Append("Idempotence-Key", idempotenceKey);
        }

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        var actionArguments = new Dictionary<string, object?>();
        if (argumentValue is not null)
        {
            actionArguments.Add("request", argumentValue);
        }

        return new ActionExecutingContext(
            actionContext,
            filters: [],
            actionArguments: actionArguments,
            controller: new object());
    }

    private static string ComputeTestHash(object? argumentValue)
    {
        var arguments = new Dictionary<string, object?>();
        if (argumentValue is not null)
        {
            arguments.Add("request", argumentValue);
        }
        
        var json = JsonSerializer.Serialize((IDictionary<string, object?>)arguments);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }
}
