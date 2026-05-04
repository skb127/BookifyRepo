using System.Linq.Expressions;
using Bookify.Application.Abstractions.Identity;
using Bookify.Application.Exceptions;
using Bookify.Application.Users.PasswordReset;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bookify.Application.UnitTests.Users;

public class PasswordResetTests
{
    private static readonly PasswordResetCommand Command = new("token", "new-password");

    private readonly IUserRepository _userRepositoryMock;
    private readonly IIdentityProvider _identityProviderMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly PasswordResetCommandHandler _handler;

    public PasswordResetTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _identityProviderMock = Substitute.For<IIdentityProvider>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();

        _handler = new PasswordResetCommandHandler(
            _userRepositoryMock,
            _unitOfWorkMock,
            _identityProviderMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserNotFound()
    {
        // Arrange
        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns((User?)null);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidToken);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenTokenIsExpired()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)));

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
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidToken);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenTokenIsValid()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)));

        var validToken = PasswordResetToken.Create(
            user.Id,
            "hash",
            DateTimeOffset.UtcNow.AddMinutes(30));

        typeof(User)
            .GetProperty(nameof(User.PasswordResetToken))!
            .SetValue(user, validToken);

        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns(user);

        _identityProviderMock.ResetPasswordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _identityProviderMock.Received(1).ResetPasswordAsync(user.IdentityId, Command.NewPassword, Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenIdentityProviderFails()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)));

        var validToken = PasswordResetToken.Create(
            user.Id,
            "hash",
            DateTimeOffset.UtcNow.AddMinutes(30));

        typeof(User)
            .GetProperty(nameof(User.PasswordResetToken))!
            .SetValue(user, validToken);

        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns(user);

        _identityProviderMock.ResetPasswordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(UserErrors.PasswordResetFailed));

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.PasswordResetFailed);
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenUnitOfWorkThrows()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)));

        var validToken = PasswordResetToken.Create(
            user.Id,
            "hash",
            DateTimeOffset.UtcNow.AddMinutes(30));

        typeof(User)
            .GetProperty(nameof(User.PasswordResetToken))!
            .SetValue(user, validToken);

        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns(user);

        _identityProviderMock.ResetPasswordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        _unitOfWorkMock.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConcurrencyException("Concurrency", new InvalidOperationException()));

        // Act
        Func<Task> act = async () => await _handler.Handle(Command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConcurrencyException>()
            .WithMessage("Concurrency");
    }
}
