using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Caching;
using Bookify.Application.Abstractions.Identity;
using Bookify.Application.Abstractions.Scheduling;
using Bookify.Application.Options;
using Bookify.Application.Users.RequestAccountDeletion;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Bookify.Domain.Users;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Bookify.Application.UnitTests.Users;

public class RequestAccountDeletionTests
{
    private static readonly RequestAccountDeletionCommand Command = new();

    private readonly IUserContext _userContextMock;
    private readonly IUserRepository _userRepositoryMock;
    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IJobScheduler _jobSchedulerMock;
    private readonly IIdentityProvider _identityProviderMock;
    private readonly ICacheService _cacheServiceMock;
    private readonly IOptions<AccountDeletionOptions> _optionsMock;
    private readonly RequestAccountDeletionCommandHandler _handler;

    public RequestAccountDeletionTests()
    {
        _userContextMock = Substitute.For<IUserContext>();
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _jobSchedulerMock = Substitute.For<IJobScheduler>();
        _identityProviderMock = Substitute.For<IIdentityProvider>();
        _cacheServiceMock = Substitute.For<ICacheService>();

        _optionsMock = Microsoft.Extensions.Options.Options.Create(new AccountDeletionOptions
        {
            GracePeriodHours = 72
        });

        _handler = new RequestAccountDeletionCommandHandler(
            _userContextMock,
            _userRepositoryMock,
            _bookingRepositoryMock,
            _unitOfWorkMock,
            _jobSchedulerMock,
            _identityProviderMock,
            _cacheServiceMock,
            _optionsMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userContextMock.UserId.Returns(userId);
        _userRepositoryMock.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserHasActiveBookingsAsGuest()
    {
        // Arrange
        User user = UserData.Create(Role.Guest);
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock.GetByIdWithRolesAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        _bookingRepositoryMock.HasActiveBookingsAsGuestAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.HasActiveBookingsAsGuest);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserHasActiveBookingsAsHost()
    {
        // Arrange
        User user = UserData.Create(Role.Host);
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock.GetByIdWithRolesAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        _bookingRepositoryMock.HasActiveBookingsAsGuestAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(false);
        _bookingRepositoryMock.HasActiveBookingsAsHostAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.HasActiveBookingsAsHost);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_AndSetPendingDeletion_WhenNoActiveBookings()
    {
        // Arrange
        User user = UserData.Create(Role.Guest);
        user.SetIdentityId("identity-123");
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock.GetByIdWithRolesAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        _bookingRepositoryMock.HasActiveBookingsAsGuestAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.PendingDeletion);
        await _identityProviderMock.Received(1).LogoutAllSessionsAsync(user.IdentityId, Arg.Any<CancellationToken>());
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldScheduleAccountDeletionJob_WhenNoActiveBookings()
    {
        // Arrange
        User user = UserData.Create(Role.Guest);
        user.SetIdentityId("identity-123");
        _userContextMock.UserId.Returns(user.Id);
        _userRepositoryMock.GetByIdWithRolesAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        _bookingRepositoryMock.HasActiveBookingsAsGuestAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _handler.Handle(Command, CancellationToken.None);

        // Assert
        await _jobSchedulerMock.Received(1).ScheduleAccountDeletionAsync(
            user.Id,
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }
}
