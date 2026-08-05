using Bookify.Application.Abstractions.Caching;
using Bookify.Application.Abstractions.Scheduling;
using Bookify.Application.Users.CancelAccountDeletion;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Shared;
using Bookify.Domain.Users;
using FluentAssertions;
using NSubstitute;

namespace Bookify.Application.UnitTests.Users;

public class CancelAccountDeletionTests
{
    private static readonly CancelAccountDeletionCommand Command = new("raw-token-value");

    private readonly IUserRepository _userRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IJobScheduler _jobSchedulerMock;
    private readonly ICacheService _cacheServiceMock;
    private readonly CancelAccountDeletionCommandHandler _handler;

    public CancelAccountDeletionTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _jobSchedulerMock = Substitute.For<IJobScheduler>();
        _cacheServiceMock = Substitute.For<ICacheService>();

        _handler = new CancelAccountDeletionCommandHandler(
            _userRepositoryMock,
            _unitOfWorkMock,
            _jobSchedulerMock,
            _cacheServiceMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenTokenInvalid()
    {
        // Arrange
        _userRepositoryMock.FindByAccountDeletionTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidToken);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenDeletionWindowExpired()
    {
        // Arrange
        User user = UserData.Create(Role.Guest);
        // Create user with expired deletion token (0 hours grace period)
        user.RequestDeletion("raw-token", SecurityUtils.ComputeSha256Hash("raw-token"), 0);

        _userRepositoryMock.FindByAccountDeletionTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.DeletionWindowExpired);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_RestoreActiveStatus_AndCancelJob_WhenValid()
    {
        // Arrange
        User user = UserData.Create(Role.Guest);
        string rawToken = "raw-token";
        string hash = SecurityUtils.ComputeSha256Hash(rawToken);
        user.RequestDeletion(rawToken, hash, 72);

        _userRepositoryMock.FindByAccountDeletionTokenAsync(hash, Arg.Any<CancellationToken>())
            .Returns(user);

        var command = new CancelAccountDeletionCommand(rawToken);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Active);
        user.DeletionScheduledAt.Should().BeNull();
        await _jobSchedulerMock.Received(1).CancelAccountDeletionAsync(user.Id, Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
