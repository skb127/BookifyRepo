using Bookify.Domain.UnitTests.Infrastructure;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using FluentAssertions;

namespace Bookify.Domain.UnitTests.Users;

public class UserDeactivationTests : BaseTest
{
    [Fact]
    public void RequestDeletion_ShouldSetStatusToPendingDeletion()
    {
        // Arrange
        User user = UserData.CreateUser();

        // Act
        user.RequestDeletion("token", "hash", 72);

        // Assert
        user.Status.Should().Be(UserStatus.PendingDeletion);
    }

    [Fact]
    public void RequestDeletion_ShouldSetDeletionScheduledAt()
    {
        // Arrange
        User user = UserData.CreateUser();

        // Act
        user.RequestDeletion("token", "hash", 72);

        // Assert
        user.DeletionScheduledAt.Should().NotBeNull();
        user.DeletionScheduledAt!.Value.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void RequestDeletion_ShouldRaiseUserAccountDeletionRequestedDomainEvent()
    {
        // Arrange
        User user = UserData.CreateUser();

        // Act
        user.RequestDeletion("token", "hash", 72);

        // Assert
        var domainEvent = AssertDomainEventWasPublished<UserAccountDeletionRequestedDomainEvent>(user);
        domainEvent.UserId.Should().Be(user.Id);
    }

    [Fact]
    public void CancelDeletion_ShouldRestoreStatusToActive()
    {
        // Arrange
        User user = UserData.CreateUser();
        user.RequestDeletion("token", "hash", 72);

        // Act
        user.CancelDeletion();

        // Assert
        user.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void CancelDeletion_ShouldClearDeletionScheduledAt()
    {
        // Arrange
        User user = UserData.CreateUser();
        user.RequestDeletion("token", "hash", 72);

        // Act
        user.CancelDeletion();

        // Assert
        user.DeletionScheduledAt.Should().BeNull();
    }

    [Fact]
    public void CancelDeletion_ShouldRaiseUserAccountDeletionCancelledDomainEvent()
    {
        // Arrange
        User user = UserData.CreateUser();
        user.RequestDeletion("token", "hash", 72);

        // Act
        user.CancelDeletion();

        // Assert
        var domainEvent = AssertDomainEventWasPublished<UserAccountDeletionCancelledDomainEvent>(user);
        domainEvent.UserId.Should().Be(user.Id);
    }

    [Fact]
    public void Delete_ShouldSetStatusToDeleted()
    {
        // Arrange
        User user = UserData.CreateUser();

        // Act
        user.Delete();

        // Assert
        user.Status.Should().Be(UserStatus.Deleted);
    }

    [Fact]
    public void Delete_ShouldSetDeletedAt()
    {
        // Arrange
        User user = UserData.CreateUser();

        // Act
        user.Delete();

        // Assert
        user.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Delete_ShouldRaiseUserDeletedDomainEvent()
    {
        // Arrange
        User user = UserData.CreateUser();

        // Act
        user.Delete();

        // Assert
        var domainEvent = AssertDomainEventWasPublished<UserDeletedDomainEvent>(user);
        domainEvent.UserId.Should().Be(user.Id);
    }
}
