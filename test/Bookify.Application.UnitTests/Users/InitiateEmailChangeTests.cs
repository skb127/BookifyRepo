using System.Linq.Expressions;
using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Identity;
using Bookify.Application.Exceptions;
using Bookify.Application.Options;
using Bookify.Application.Users.InitiateEmailChange;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bookify.Application.UnitTests.Users;

public class InitiateEmailChangeTests
{
    private static readonly InitiateEmailChangeCommand Command = new("new@test.com", "password");

    private readonly IIdentityProvider _identityProviderMock;
    private readonly IUserContext _userContextMock;
    private readonly IUserRepository _userRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IOptions<ExpirationOptions> _expirationOptionsMock;
    private readonly InitiateEmailChangeCommandHandler _handler;

    public InitiateEmailChangeTests()
    {
        _identityProviderMock = Substitute.For<IIdentityProvider>();
        _userContextMock = Substitute.For<IUserContext>();
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _expirationOptionsMock = Substitute.For<IOptions<ExpirationOptions>>();
        _expirationOptionsMock.Value.Returns(new ExpirationOptions { EmailChangeExpirationSeconds = 1800 });

        _handler = new InitiateEmailChangeCommandHandler(
            _identityProviderMock,
            _userContextMock,
            _userRepositoryMock,
            _unitOfWorkMock,
            _expirationOptionsMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserNotFound()
    {
        // Arrange
        _userContextMock.UserId.Returns(Guid.NewGuid());

        _userRepositoryMock
            .GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns((User?)null);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.Error.Should().Be(UserErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenCurrentPasswordIsInvalid()
    {
        // Arrange
        User user = UserData.Create();

        _userContextMock.UserId.Returns(user.Id);
        _userContextMock.Email.Returns(user.Email.Value);

        _userRepositoryMock
            .GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns(user);

        _identityProviderMock
            .ValidateCredentialsAsync(user.Email.Value, Command.CurrentPassword, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.Error.Should().Be(UserErrors.InvalidCurrentCredentials);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenEmailAlreadyExistsInDb()
    {
        // Arrange
        User user = UserData.Create();

        _userContextMock.UserId.Returns(user.Id);
        _userContextMock.Email.Returns(user.Email.Value);

        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns(user);

        _identityProviderMock.ValidateCredentialsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Simulate collision in DB
        _userRepositoryMock.FindOneAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(User.Create(new FirstName("Other"), new LastName("Person"), new Email(Command.NewEmail), DateOfBirth.Create(new DateOnly(2000, 1, 1))));

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.Error.Should().Be(UserErrors.EmailAlreadyInUse);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenEmailAlreadyExistsInIdp()
    {
        // Arrange
        User user = UserData.Create();

        _userContextMock.UserId.Returns(user.Id);
        _userContextMock.Email.Returns(user.Email.Value);

        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns(user);

        _identityProviderMock.ValidateCredentialsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _userRepositoryMock.FindOneAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        _identityProviderMock.CheckUserByEmailExistsAsync(Command.NewEmail, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.Error.Should().Be(UserErrors.EmailAlreadyInUse);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenPendingEmailChangeRequestExistsAndIsValid()
    {
        // Arrange
        User user = UserData.Create();

        _userContextMock.UserId.Returns(user.Id);
        _userContextMock.Email.Returns(user.Email.Value);

        // Add valid token
        var validToken = EmailChangeToken.Create(user.Id, new Email("new@test.com"), "hash", DateTimeOffset.UtcNow.AddMinutes(30));
        typeof(User).GetProperty(nameof(User.EmailChangeToken))!.SetValue(user, validToken);

        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns(user);

        _identityProviderMock.ValidateCredentialsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _userRepositoryMock.FindOneAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        _identityProviderMock.CheckUserByEmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _userRepositoryMock.Received(1).AddEmailChangeToken(user);
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenPendingEmailChangeRequestExistsButIsExpired()
    {
        // Arrange
        User user = UserData.Create();

        _userContextMock.UserId.Returns(user.Id);
        _userContextMock.Email.Returns(user.Email.Value);

        // Add expired token
        var expiredToken = EmailChangeToken.Create(user.Id, new Email("old-pending@test.com"), "hash", DateTimeOffset.UtcNow.AddMinutes(-30));
        typeof(User).GetProperty(nameof(User.EmailChangeToken))!.SetValue(user, expiredToken);

        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns(user);

        _identityProviderMock.ValidateCredentialsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _userRepositoryMock.FindOneAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        _identityProviderMock.CheckUserByEmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _userRepositoryMock.Received(1).AddEmailChangeToken(user);
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenAllChecksPass()
    {
        // Arrange
        User user = UserData.Create();

        _userContextMock.UserId.Returns(user.Id);
        _userContextMock.Email.Returns(user.Email.Value);

        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns(user);

        _identityProviderMock.ValidateCredentialsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _userRepositoryMock.FindOneAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        _identityProviderMock.CheckUserByEmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();


        _userRepositoryMock.Received(1).AddEmailChangeToken(user);
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenUnitOfWorkThrows()
    {
        // Arrange
        User user = UserData.Create();

        _userContextMock.UserId.Returns(user.Id);
        _userContextMock.Email.Returns(user.Email.Value);

        _userRepositoryMock.GetOneWithIncludesAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, object?>>>())
            .Returns(user);

        _identityProviderMock.ValidateCredentialsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _userRepositoryMock.FindOneAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        _identityProviderMock.CheckUserByEmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _unitOfWorkMock.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConcurrencyException("Concurrency", new InvalidOperationException()));

        // Act
        Func<Task> act = async () => await _handler.Handle(Command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConcurrencyException>()
            .WithMessage("Concurrency");
    }
}