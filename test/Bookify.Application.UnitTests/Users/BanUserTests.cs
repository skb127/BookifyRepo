using Bookify.Application.Abstractions.Caching;
using Bookify.Application.Users.BanUser;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;
using FluentAssertions;
using NSubstitute;

namespace Bookify.Application.UnitTests.Users;

public class BanUserTests
{
    private readonly IUserRepository _userRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly ICacheService _cacheServiceMock;
    private readonly BanUserCommandHandler _handler;

    public BanUserTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _cacheServiceMock = Substitute.For<ICacheService>();

        _handler = new BanUserCommandHandler(
            _userRepositoryMock,
            _unitOfWorkMock,
            _cacheServiceMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new BanUserCommand(userId);

        _userRepositoryMock.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserIsAdmin()
    {
        // Arrange
        User user = UserData.Create(Role.Admin);
        var command = new BanUserCommand(user.Id);

        _userRepositoryMock.GetByIdWithRolesAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.CannotBanAdmin);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserIsAlreadySuspended()
    {
        // Arrange
        User user = UserData.Create(Role.Guest);
        user.Ban();
        var command = new BanUserCommand(user.Id);

        _userRepositoryMock.GetByIdWithRolesAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.AlreadySuspended);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_AndBanUser_WhenValid()
    {
        // Arrange
        User user = UserData.Create(Role.Guest);
        var command = new BanUserCommand(user.Id);

        _userRepositoryMock.GetByIdWithRolesAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Suspended);
        user.BanCount.Should().Be(1);
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
