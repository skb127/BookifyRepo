using Bookify.Domain.Abstractions;
using Bookify.Application.Abstractions.Payments;
using Bookify.Application.Users.RegisterUser;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bookify.Application.UnitTests.Users.Events;

public class UserCreatedStripeCustomerSyncHandlerTests
{
    private readonly IUserRepository _userRepositoryMock;
    private readonly IStripeCustomerService _stripeCustomerServiceMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly ILogger<UserCreatedStripeCustomerSyncHandler> _loggerMock;
    private readonly UserCreatedStripeCustomerSyncHandler _handler;

    public UserCreatedStripeCustomerSyncHandlerTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _stripeCustomerServiceMock = Substitute.For<IStripeCustomerService>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _loggerMock = Substitute.For<ILogger<UserCreatedStripeCustomerSyncHandler>>();

        _handler = new UserCreatedStripeCustomerSyncHandler(
            _userRepositoryMock,
            _stripeCustomerServiceMock,
            _unitOfWorkMock,
            _loggerMock);
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenUserNotFound()
    {
        // Arrange
        var domainEvent = new UserCreatedDomainEvent(Guid.NewGuid());
        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _stripeCustomerServiceMock.DidNotReceive()
            .UpsertCustomerAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCallUpsertWithCorrectArgs_WhenUserFound()
    {
        // Arrange
        var user = User.Create(
            new FirstName("First"),
            new LastName("Last"),
            new Email("test@test.com"),
            DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        var domainEvent = new UserCreatedDomainEvent(user.Id);

        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        _stripeCustomerServiceMock.UpsertCustomerAsync(
                user.Id,
                user.Email.Value,
                $"{user.FirstName.Value} {user.LastName.Value}",
                Arg.Any<CancellationToken>())
            .Returns("cus_123456");

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _stripeCustomerServiceMock.Received(1).UpsertCustomerAsync(
            user.Id,
            user.Email.Value,
            $"{user.FirstName.Value} {user.LastName.Value}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSetStripeCustomerIdOnUser_WhenUpsertSucceeds()
    {
        // Arrange
        var user = User.Create(
            new FirstName("First"),
            new LastName("Last"),
            new Email("test@test.com"),
            DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        var domainEvent = new UserCreatedDomainEvent(user.Id);

        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        _stripeCustomerServiceMock.UpsertCustomerAsync(
                user.Id,
                user.Email.Value,
                $"{user.FirstName.Value} {user.LastName.Value}",
                Arg.Any<CancellationToken>())
            .Returns("cus_123456");

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        user.StripeCustomerId.Should().Be("cus_123456");
    }

    [Fact]
    public async Task Handle_ShouldSaveChanges_WhenUpsertSucceeds()
    {
        // Arrange
        var user = User.Create(
            new FirstName("First"),
            new LastName("Last"),
            new Email("test@test.com"),
            DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        var domainEvent = new UserCreatedDomainEvent(user.Id);

        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        _stripeCustomerServiceMock.UpsertCustomerAsync(
                user.Id,
                user.Email.Value,
                $"{user.FirstName.Value} {user.LastName.Value}",
                Arg.Any<CancellationToken>())
            .Returns("cus_123456");

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
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
        var domainEvent = new UserCreatedDomainEvent(user.Id);

        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        _stripeCustomerServiceMock.UpsertCustomerAsync(
                user.Id,
                user.Email.Value,
                $"{user.FirstName.Value} {user.LastName.Value}",
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Stripe API is down"));

        // Act
        Func<Task> act = async () => await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
