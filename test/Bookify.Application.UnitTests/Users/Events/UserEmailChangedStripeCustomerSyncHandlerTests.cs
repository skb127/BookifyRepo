using Bookify.Application.Abstractions.Payments;
using Bookify.Application.Users.ConfirmEmailChange;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bookify.Application.UnitTests.Users.Events;

public class UserEmailChangedStripeCustomerSyncHandlerTests
{
    private readonly IUserRepository _userRepositoryMock;
    private readonly IStripeCustomerService _stripeCustomerServiceMock;
    private readonly ILogger<UserEmailChangedStripeCustomerSyncHandler> _loggerMock;
    private readonly UserEmailChangedStripeCustomerSyncHandler _handler;

    public UserEmailChangedStripeCustomerSyncHandlerTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _stripeCustomerServiceMock = Substitute.For<IStripeCustomerService>();
        _loggerMock = Substitute.For<ILogger<UserEmailChangedStripeCustomerSyncHandler>>();

        _handler = new UserEmailChangedStripeCustomerSyncHandler(
            _userRepositoryMock,
            _stripeCustomerServiceMock,
            _loggerMock);
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenUserNotFound()
    {
        // Arrange
        var domainEvent = new UserEmailChangedDomainEvent(Guid.NewGuid(), "old@test.com", "new@test.com");
        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _stripeCustomerServiceMock.DidNotReceive()
            .UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldLogWarningAndSkip_WhenStripeCustomerIdIsNull()
    {
        // Arrange
        var user = User.Create(
            new FirstName("First"),
            new LastName("Last"),
            new Email("new@test.com"),
            DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        var domainEvent = new UserEmailChangedDomainEvent(user.Id, "old@test.com", "new@test.com");

        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _stripeCustomerServiceMock.DidNotReceive()
            .UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCallUpdateWithNewEmail_WhenStripeCustomerIdExists()
    {
        // Arrange
        var user = User.Create(
            new FirstName("First"),
            new LastName("Last"),
            new Email("new@test.com"),
            DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        user.SetStripeCustomerId("cus_123");
        var domainEvent = new UserEmailChangedDomainEvent(user.Id, "old@test.com", "new@test.com");

        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _stripeCustomerServiceMock.Received(1).UpdateCustomerAsync(
            "cus_123",
            "new@test.com",
            $"{user.FirstName.Value} {user.LastName.Value}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldLogErrorAndNotPropagate_WhenStripeThrows()
    {
        // Arrange
        var user = User.Create(
            new FirstName("First"),
            new LastName("Last"),
            new Email("new@test.com"),
            DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        user.SetStripeCustomerId("cus_123");
        var domainEvent = new UserEmailChangedDomainEvent(user.Id, "old@test.com", "new@test.com");

        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        _stripeCustomerServiceMock.UpdateCustomerAsync(
                "cus_123",
                "new@test.com",
                $"{user.FirstName.Value} {user.LastName.Value}",
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Stripe connection error"));

        // Act
        Func<Task> act = async () => await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
