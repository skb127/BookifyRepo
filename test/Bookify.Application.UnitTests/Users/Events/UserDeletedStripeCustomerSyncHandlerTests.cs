using Bookify.Application.Abstractions.Payments;
using Bookify.Application.Users.DeleteUser;
using Bookify.Domain.Users.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bookify.Application.UnitTests.Users.Events;

public class UserDeletedStripeCustomerSyncHandlerTests
{
    private readonly IStripeCustomerService _stripeCustomerServiceMock;
    private readonly ILogger<UserDeletedStripeCustomerSyncHandler> _loggerMock;
    private readonly UserDeletedStripeCustomerSyncHandler _handler;

    public UserDeletedStripeCustomerSyncHandlerTests()
    {
        _stripeCustomerServiceMock = Substitute.For<IStripeCustomerService>();
        _loggerMock = Substitute.For<ILogger<UserDeletedStripeCustomerSyncHandler>>();

        _handler = new UserDeletedStripeCustomerSyncHandler(
            _stripeCustomerServiceMock,
            _loggerMock);
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenStripeCustomerIdInEventIsNull()
    {
        // Arrange
        var domainEvent = new UserDeletedDomainEvent(Guid.NewGuid(), null);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _stripeCustomerServiceMock.DidNotReceive()
            .DeactivateCustomerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenStripeCustomerIdInEventIsEmpty()
    {
        // Arrange
        var domainEvent = new UserDeletedDomainEvent(Guid.NewGuid(), " ");

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _stripeCustomerServiceMock.DidNotReceive()
            .DeactivateCustomerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCallDeactivateWithCorrectId_WhenStripeCustomerIdExists()
    {
        // Arrange
        var domainEvent = new UserDeletedDomainEvent(Guid.NewGuid(), "cus_999");

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _stripeCustomerServiceMock.Received(1)
            .DeactivateCustomerAsync("cus_999", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldLogErrorAndNotPropagate_WhenStripeThrows()
    {
        // Arrange
        var domainEvent = new UserDeletedDomainEvent(Guid.NewGuid(), "cus_999");
        _stripeCustomerServiceMock.DeactivateCustomerAsync("cus_999", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Stripe down"));

        // Act
        Func<Task> act = async () => await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
