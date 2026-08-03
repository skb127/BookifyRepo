using Bookify.Application.Abstractions.Payments;
using Bookify.Application.Users.UpdateUserProfile;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bookify.Application.UnitTests.Users.Events;

public class UserProfileUpdatedStripeCustomerSyncHandlerTests
{
    private readonly IUserRepository _userRepositoryMock;
    private readonly IStripeCustomerService _stripeCustomerServiceMock;
    private readonly ILogger<UserProfileUpdatedStripeCustomerSyncHandler> _loggerMock;
    private readonly UserProfileUpdatedStripeCustomerSyncHandler _handler;

    public UserProfileUpdatedStripeCustomerSyncHandlerTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _stripeCustomerServiceMock = Substitute.For<IStripeCustomerService>();
        _loggerMock = Substitute.For<ILogger<UserProfileUpdatedStripeCustomerSyncHandler>>();

        _handler = new UserProfileUpdatedStripeCustomerSyncHandler(
            _userRepositoryMock,
            _stripeCustomerServiceMock,
            _loggerMock);
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenUserNotFound()
    {
        // Arrange
        var domainEvent = new UserProfileUpdatedDomainEvent(Guid.NewGuid());
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
            new Email("test@test.com"),
            DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        var domainEvent = new UserProfileUpdatedDomainEvent(user.Id);

        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _stripeCustomerServiceMock.DidNotReceive()
            .UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldLogWarningAndSkip_WhenStripeCustomerIdIsEmpty()
    {
        // Arrange
        var user = User.Create(
            new FirstName("First"),
            new LastName("Last"),
            new Email("test@test.com"),
            DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        user.SetStripeCustomerId(" ");
        var domainEvent = new UserProfileUpdatedDomainEvent(user.Id);

        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _stripeCustomerServiceMock.DidNotReceive()
            .UpdateCustomerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCallUpdateWithCorrectArgs_WhenStripeCustomerIdExists()
    {
        // Arrange
        var user = User.Create(
            new FirstName("First"),
            new LastName("Last"),
            new Email("test@test.com"),
            DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        user.SetStripeCustomerId("cus_123");
        var domainEvent = new UserProfileUpdatedDomainEvent(user.Id);

        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _stripeCustomerServiceMock.Received(1).UpdateCustomerAsync(
            "cus_123",
            user.Email.Value,
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
            new Email("test@test.com"),
            DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        user.SetStripeCustomerId("cus_123");
        var domainEvent = new UserProfileUpdatedDomainEvent(user.Id);

        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        _stripeCustomerServiceMock.UpdateCustomerAsync(
                "cus_123",
                user.Email.Value,
                $"{user.FirstName.Value} {user.LastName.Value}",
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Stripe connection error"));

        // Act
        Func<Task> act = async () => await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
