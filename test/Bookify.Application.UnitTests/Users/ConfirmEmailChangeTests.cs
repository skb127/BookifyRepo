using System.Linq.Expressions;
using Bookify.Application.Abstractions.Identity;
using Bookify.Application.Exceptions;
using Bookify.Application.Users.ConfirmEmailChange;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bookify.Application.UnitTests.Users;

public class ConfirmEmailChangeTests
{
    private static readonly ConfirmEmailChangeCommand Command = new("token");

    private readonly IUserRepository _userRepositoryMock;
    private readonly IIdentityProvider _identityProviderMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly ConfirmEmailChangeCommandHandler _handler;

    public ConfirmEmailChangeTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _identityProviderMock = Substitute.For<IIdentityProvider>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();

        _handler = new ConfirmEmailChangeCommandHandler(
            _userRepositoryMock,
            _identityProviderMock,
            _unitOfWorkMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserNotFound()
    {
        // Arrange
        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns((User?)null);

        // Act
        var result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidEmailChangeToken);
        await _identityProviderMock.DidNotReceive().UpdateUserEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserFound_ButTokenIsNull()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        // No email change requested, so Token is null

        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns(user);

        // Act
        var result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidEmailChangeToken);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserFound_ButTokenIsExpired()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);

        // Create an expired token directly since the constructor is now public
        var expiredToken = EmailChangeToken.Create(
            user.Id,
            new Email("new@test.com"),
            "hash",
            DateTimeOffset.UtcNow.AddMinutes(-30));

        typeof(User)
            .GetProperty(nameof(User.EmailChangeToken))!
            .SetValue(user, expiredToken);

        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns(user);

        // Act
        var result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidEmailChangeToken);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenTokenCorrect()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        var newEmail = new Email("new@test.com");
        user.RequestEmailChange(newEmail, TimeSpan.FromMinutes(30));

        // We return the user when searched by hash
        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns(user);

        _identityProviderMock.UpdateUserEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        _identityProviderMock.LogoutAllSessionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        user.Email.Should().Be(newEmail);
        user.EmailChangeToken.Should().BeNull();

        await _identityProviderMock.Received(1).UpdateUserEmailAsync(user.IdentityId, newEmail.Value, Arg.Any<CancellationToken>());
        await _identityProviderMock.Received(1).LogoutAllSessionsAsync(user.IdentityId, Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenIdentityProviderFails()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        var newEmail = new Email("new@test.com");
        user.RequestEmailChange(newEmail, TimeSpan.FromMinutes(30));

        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns(user);

        // Simulate IdP failure
        _identityProviderMock.UpdateUserEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new Error("IdP.Error", "Failed to update email in IdP")));

        // Act
        var result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidEmailChangeToken);

        // Verify that NO changes were saved to the DB and the user remains the same
        user.Email.Value.Should().Be("test@test.com");
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenUnitOfWorkThrows()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        var newEmail = new Email("new@test.com");
        user.RequestEmailChange(newEmail, TimeSpan.FromMinutes(30));

        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns(user);

        _identityProviderMock.UpdateUserEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        _identityProviderMock.LogoutAllSessionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
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
