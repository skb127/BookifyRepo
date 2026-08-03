using Bookify.Domain.UnitTests.Infrastructure;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using FluentAssertions;

namespace Bookify.Domain.UnitTests.Users;

public class UserBanTests : BaseTest
{
    [Fact]
    public void Ban_ShouldSetStatusToSuspended()
    {
        // Arrange
        User user = UserData.CreateUser();

        // Act
        user.Ban();

        // Assert
        user.Status.Should().Be(UserStatus.Suspended);
    }

    [Fact]
    public void Ban_ShouldIncrementBanCount_OnFirstBan()
    {
        // Arrange
        User user = UserData.CreateUser();

        // Act
        user.Ban();

        // Assert
        user.BanCount.Should().Be(1);
    }

    [Fact]
    public void Ban_ShouldIncrementBanCount_OnSubsequentBans()
    {
        // Arrange
        User user = UserData.CreateUser();
        user.Ban();
        user.Unban();

        // Act
        user.Ban();

        // Assert
        user.BanCount.Should().Be(2);
    }

    [Fact]
    public void Ban_ShouldRaiseUserBannedDomainEvent()
    {
        // Arrange
        User user = UserData.CreateUser();

        // Act
        user.Ban();

        // Assert
        var domainEvent = AssertDomainEventWasPublished<UserBannedDomainEvent>(user);
        domainEvent.UserId.Should().Be(user.Id);
    }

    [Fact]
    public void Unban_ShouldRestoreStatusToActive()
    {
        // Arrange
        User user = UserData.CreateUser();
        user.Ban();

        // Act
        user.Unban();

        // Assert
        user.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void Unban_ShouldNotResetBanCount()
    {
        // Arrange
        User user = UserData.CreateUser();
        user.Ban();

        // Act
        user.Unban();

        // Assert
        user.BanCount.Should().Be(1);
    }

    [Fact]
    public void Unban_ShouldRaiseUserUnbannedDomainEvent()
    {
        // Arrange
        User user = UserData.CreateUser();
        user.Ban();

        // Act
        user.Unban();

        // Assert
        var domainEvent = AssertDomainEventWasPublished<UserUnbannedDomainEvent>(user);
        domainEvent.UserId.Should().Be(user.Id);
    }
}
