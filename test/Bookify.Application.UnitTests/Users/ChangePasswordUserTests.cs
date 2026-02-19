using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Identity;
using Bookify.Application.Exceptions;
using Bookify.Application.Users.ChangePasswordUser;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bookify.Application.UnitTests.Users;

public class ChangePasswordUserTests
{
    private static readonly ChangePasswordUserCommand Command = new("CurrentPassword", "NewPassword");

    private readonly IIdentityProvider _identityProviderMock;
    private readonly IUserContext _userContextMock;
    private readonly IUserRepository _userRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly ChangePasswordUserCommandHandler _handler;

    public ChangePasswordUserTests()
    {
        _identityProviderMock = Substitute.For<IIdentityProvider>();
        _userContextMock = Substitute.For<IUserContext>();
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();

        _handler = new ChangePasswordUserCommandHandler(
            _identityProviderMock,
            _userContextMock,
            _userRepositoryMock,
            _unitOfWorkMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserNotFound()
    {
        // Arrange
        _userContextMock.UserId.Returns(Guid.NewGuid());
        _userRepositoryMock.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenCurrentPasswordIsInvalid()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1))!);
        _userContextMock.UserId.Returns(user.Id);
        _userContextMock.Email.Returns(user.Email.Value); // Mock Email property
        _userRepositoryMock.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        _identityProviderMock.ValidateCredentialsAsync(user.Email.Value, Command.CurrentPassword, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidCurrentCredentials);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenIdPResetFails()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1))!);
        _userContextMock.UserId.Returns(user.Id);
        _userContextMock.Email.Returns(user.Email.Value);
        _userRepositoryMock.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        _identityProviderMock.ValidateCredentialsAsync(user.Email.Value, Command.CurrentPassword, Arg.Any<CancellationToken>())
            .Returns(true);

        _identityProviderMock.ResetPasswordAsync(user.IdentityId, Command.NewPassword, Arg.Any<CancellationToken>())
            .Returns(Result.Failure(UserErrors.PasswordChangeFailed));

        // Act
        var result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.PasswordChangeFailed);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenPasswordChanged()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1))!);
        _userContextMock.UserId.Returns(user.Id);
        _userContextMock.Email.Returns(user.Email.Value);
        _userRepositoryMock.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        _identityProviderMock.ValidateCredentialsAsync(user.Email.Value, Command.CurrentPassword, Arg.Any<CancellationToken>())
            .Returns(true);

        _identityProviderMock.ResetPasswordAsync(user.IdentityId, Command.NewPassword, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenUnitOfWorkThrows()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1))!);
        _userContextMock.UserId.Returns(user.Id);
        _userContextMock.Email.Returns(user.Email.Value);
        _userRepositoryMock.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        _identityProviderMock.ValidateCredentialsAsync(user.Email.Value, Command.CurrentPassword, Arg.Any<CancellationToken>())
            .Returns(true);

        _identityProviderMock.ResetPasswordAsync(user.IdentityId, Command.NewPassword, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        _unitOfWorkMock.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConcurrencyException("Concurrency", new InvalidOperationException()));

        // Act
        Func<Task> act = async () => await _handler.Handle(Command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConcurrencyException>();
    }
}
