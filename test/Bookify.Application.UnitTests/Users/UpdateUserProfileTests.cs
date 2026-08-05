using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Identity;
using Bookify.Application.Exceptions;
using Bookify.Application.Users.UpdateUserProfile;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bookify.Application.UnitTests.Users;

public class UpdateUserProfileTests
{
    private static readonly UpdateUserProfileCommand Command = new(
        "FirstName",
        "LastName",
        "123456789",
        new DateOnly(2000, 1, 1),
        "Password");

    private readonly IUserRepository _userRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IUserContext _userContextMock;
    private readonly IIdentityProvider _identityProviderMock;
    private readonly UpdateUserProfileCommandHandler _handler;

    public UpdateUserProfileTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _userContextMock = Substitute.For<IUserContext>();
        _identityProviderMock = Substitute.For<IIdentityProvider>();

        _handler = new UpdateUserProfileCommandHandler(
            _userRepositoryMock,
            _unitOfWorkMock,
            _userContextMock,
            _identityProviderMock);
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
    public async Task Handle_ShouldReturnFailure_WhenPasswordIsInvalid()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        _identityProviderMock.ValidateCredentialsAsync(user.Email.Value, Command.Password, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidCredentials);
        await _identityProviderMock.DidNotReceive().UpdateUserProfileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenIdentityProviderFails()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        _identityProviderMock.ValidateCredentialsAsync(user.Email.Value, Command.Password, Arg.Any<CancellationToken>())
            .Returns(true);

        _identityProviderMock.UpdateUserProfileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(UserErrors.UpdateFailed));

        // Act
        var result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.UpdateFailed);
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenUpdateIsValid()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        _identityProviderMock.ValidateCredentialsAsync(user.Email.Value, Command.Password, Arg.Any<CancellationToken>())
            .Returns(true);

        _identityProviderMock.UpdateUserProfileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        user.FirstName.Value.Should().Be(Command.FirstName);
        user.LastName.Value.Should().Be(Command.LastName);
        user.PhoneNumber!.Value.Should().Be(Command.PhoneNumber);
        user.DateOfBirth.Value.Should().Be(Command.DateOfBirth);

        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenPhoneNumberIsEmpty()
    {
        // Arrange
        var command = new UpdateUserProfileCommand(
            "FirstName",
            "LastName",
            null,
            new DateOnly(2000, 1, 1),
            "Password");

        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        _identityProviderMock.ValidateCredentialsAsync(user.Email.Value, command.Password, Arg.Any<CancellationToken>())
            .Returns(true);

        _identityProviderMock.UpdateUserProfileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        user.PhoneNumber.Should().BeNull();
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUnitOfWorkThrows()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        _identityProviderMock.ValidateCredentialsAsync(user.Email.Value, Command.Password, Arg.Any<CancellationToken>())
            .Returns(true);

        _identityProviderMock.UpdateUserProfileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        _unitOfWorkMock.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConcurrencyException("Concurrency", new InvalidOperationException()));

        // Act
        Func<Task> act = async () => await _handler.Handle(Command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    [Fact]
    public async Task Handle_ShouldCallRepository_WithCorrectParameters()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        _identityProviderMock.ValidateCredentialsAsync(user.Email.Value, Command.Password, Arg.Any<CancellationToken>())
            .Returns(true);

        _identityProviderMock.UpdateUserProfileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        await _handler.Handle(Command, CancellationToken.None);

        // Assert
        await _userRepositoryMock.Received(1).GetByIdAsync(
            Arg.Is<Guid>(id => id == user.Id),
            Arg.Any<CancellationToken>());
    }
}
