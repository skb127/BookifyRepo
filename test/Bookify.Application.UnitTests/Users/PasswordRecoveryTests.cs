using System.Linq.Expressions;
using Bookify.Application.Exceptions;
using Bookify.Application.Options;
using Bookify.Application.Users.PasswordRecovery;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bookify.Application.UnitTests.Users;

public class PasswordRecoveryTests
{
    private static readonly PasswordRecoveryCommand Command = new("test@test.com");

    private readonly IUserRepository _userRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IOptions<ExpirationOptions> _expirationOptionsMock;
    private readonly PasswordRecoveryCommandHandler _handler;

    public PasswordRecoveryTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _expirationOptionsMock = Substitute.For<IOptions<ExpirationOptions>>();
        _expirationOptionsMock.Value.Returns(new ExpirationOptions { PasswordRecoveryExpirationSeconds = 1800 });

        _handler = new PasswordRecoveryCommandHandler(
            _userRepositoryMock,
            _unitOfWorkMock,
            _expirationOptionsMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenUserNotFound()
    {
        // Arrange
        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns((User?)null);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _userRepositoryMock.DidNotReceive().AddPasswordResetToken(Arg.Any<User>());
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenTokenAlreadyExistsAndValid()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email(Command.Email), DateOfBirth.Create(new DateOnly(2000, 1, 1)));

        var validToken = PasswordResetToken.Create(
            user.Id,
            "hash",
            DateTimeOffset.UtcNow.AddMinutes(30));

        typeof(User)
            .GetProperty(nameof(User.PasswordResetToken))!
            .SetValue(user, validToken);

        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns(user);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _userRepositoryMock.DidNotReceive().AddPasswordResetToken(Arg.Any<User>());
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCreateTokenAndSave_WhenTokenIsNull()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email(Command.Email), DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        // Token is null by default

        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns(user);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        user.PasswordResetToken.Should().NotBeNull();
        _userRepositoryMock.Received(1).AddPasswordResetToken(user);
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCreateTokenAndSave_WhenTokenIsExpired()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email(Command.Email), DateOfBirth.Create(new DateOnly(2000, 1, 1)));

        var expiredToken = PasswordResetToken.Create(
            user.Id,
            "hash",
            DateTimeOffset.UtcNow.AddMinutes(-30));

        typeof(User)
            .GetProperty(nameof(User.PasswordResetToken))!
            .SetValue(user, expiredToken);

        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns(user);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        user.PasswordResetToken.Should().NotBeNull();
        user.PasswordResetToken!.IsExpired.Should().BeFalse(); // Should have been replaced
        _userRepositoryMock.Received(1).AddPasswordResetToken(user);
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenUnitOfWorkThrows()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email(Command.Email), DateOfBirth.Create(new DateOnly(2000, 1, 1)));

        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns(user);

        _unitOfWorkMock.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConcurrencyException("Concurrency", new InvalidOperationException()));

        // Act
        Func<Task> act = async () => await _handler.Handle(Command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConcurrencyException>()
            .WithMessage("Concurrency");
    }
}
