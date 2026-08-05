using System.Linq.Expressions;
using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Identity;
using Bookify.Application.Users.RegisterHost;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;
using FluentAssertions;
using NSubstitute;

namespace Bookify.Application.UnitTests.Users;

public class RegisterHostTests
{
    private readonly IAuthenticationService _authenticationServiceMock;
    private readonly IUserRepository _userRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IIdentityProvider _identityProviderMock;
    private readonly RegisterHostCommandHandler _handler;

    public RegisterHostTests()
    {
        _authenticationServiceMock = Substitute.For<IAuthenticationService>();
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _identityProviderMock = Substitute.For<IIdentityProvider>();

        _handler = new RegisterHostCommandHandler(
            _authenticationServiceMock,
            _userRepositoryMock,
            _unitOfWorkMock,
            _identityProviderMock);
    }

    [Fact]
    public async Task Handle_ShouldAllow_DeletedEmailReuse()
    {
        // Arrange
        var command = new RegisterHostCommand("deleted_host@test.com", "Jane", "Doe", "Password123!", new DateOnly(1990, 1, 1), "+34612345678");
        var deletedUser = UserData.Create(Role.Host);
        deletedUser.Delete();

        _userRepositoryMock.FindOneIgnoringFiltersAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(deletedUser);

        _identityProviderMock.CheckUserByEmailExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        _authenticationServiceMock.RegisterAsync(Arg.Any<User>(), command.Password, Arg.Any<CancellationToken>())
            .Returns("identity-456");

        // Act
        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _userRepositoryMock.Received(1).Add(Arg.Any<User>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenUserDoesNotExistInDbOrProvider()
    {
        // Arrange
        var command = new RegisterHostCommand("newhost@test.com", "Jane", "Doe", "Password123!", new DateOnly(1990, 1, 1), "+34612345678");

        _userRepositoryMock.FindOneIgnoringFiltersAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        _identityProviderMock.CheckUserByEmailExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        _authenticationServiceMock.RegisterAsync(Arg.Any<User>(), command.Password, Arg.Any<CancellationToken>())
            .Returns("identity-host-123");

        // Act
        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        _userRepositoryMock.Received(1).Add(Arg.Any<User>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserAlreadyExistsInProvider()
    {
        // Arrange
        var command = new RegisterHostCommand("existshostprovider@test.com", "Jane", "Doe", "Password123!", new DateOnly(1990, 1, 1), "+34612345678");

        _userRepositoryMock.FindOneIgnoringFiltersAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        _identityProviderMock.CheckUserByEmailExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.AlreadyExists);
        _userRepositoryMock.DidNotReceive().Add(Arg.Any<User>());
    }
}
