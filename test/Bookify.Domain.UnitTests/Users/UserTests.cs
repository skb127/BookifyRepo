using Bookify.Domain.Shared;
using Bookify.Domain.UnitTests.Infrastructure;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using FluentAssertions;

namespace Bookify.Domain.UnitTests.Users;

public class UserTests : BaseTest
{
    [Fact]
    public void Create_ShouldSetPropertyValues()
    {
        // Act
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        // Assert
        user.FirstName.Should().Be(UserData.FirstName);
        user.LastName.Should().Be(UserData.LastName);
        user.Email.Should().Be(UserData.Email);
    }

    [Fact]
    public void Create_ShouldRaiseUserCreateDomainEvent()
    {
        // Act
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        // Assert
        UserCreatedDomainEvent domainEvent = AssertDomainEventWasPublished<UserCreatedDomainEvent>(user);

        domainEvent.UserId.Should().Be(user.Id);
    }

    [Fact]
    public void Create_ShouldAddRegisteredRoleToUser()
    {
        // Act
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        // Assert
        user.Roles.Should().Contain(Role.Registered);
    }

    [Fact]
    public void Create_Should_SetStatusToActive()
    {
        // Act
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        // Assert
        user.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void Create_Should_SetDateOfBirth()
    {
        // Act
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        // Assert
        user.DateOfBirth.Should().Be(UserData.DateOfBirth);
    }

    [Fact]
    public void Create_Should_HaveNullPhoneNumber()
    {
        // Act
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        // Assert
        user.PhoneNumber.Should().BeNull();
    }

    [Fact]
    public void Create_Should_HaveEmptyIdentityId()
    {
        // Act
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        // Assert
        user.IdentityId.Should().BeEmpty();
    }

    [Fact]
    public void SetIdentityId_Should_UpdateIdentityId()
    {
        // Arrange
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        var identityId = "auth0|123456";

        // Act
        user.SetIdentityId(identityId);

        // Assert
        user.IdentityId.Should().Be(identityId);
    }

    [Fact]
    public void ChangePassword_Should_SetPasswordChangedAt()
    {
        // Arrange
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        // Act
        user.ChangePassword();

        // Assert
        user.PasswordChangedAt.Should().NotBeNull();
    }

    [Fact]
    public void ChangePassword_Should_RaiseUserChangedPasswordDomainEvent()
    {
        // Arrange
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        // Act
        user.ChangePassword();

        // Assert
        var domainEvent = AssertDomainEventWasPublished<UserChangedPasswordDomainEvent>(user);
        domainEvent.UserId.Should().Be(user.Id);
    }

    [Fact]
    public void RequestPasswordRecovery_Should_SetLastRecoveryRequestAt()
    {
        // Arrange
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        // Act
        user.RequestPasswordRecovery(TimeSpan.FromMinutes(30));

        // Assert
        user.LastRecoveryRequestAt.Should().NotBeNull();
    }

    [Fact]
    public void RequestPasswordRecovery_Should_CreatePasswordResetToken()
    {
        // Arrange
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        // Act
        user.RequestPasswordRecovery(TimeSpan.FromMinutes(30));

        // Assert
        user.PasswordResetToken.Should().NotBeNull();
        user.PasswordResetToken!.ExpirationUtc.Should().BeAfter(DateTime.UtcNow);
        user.PasswordResetToken.TokenHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void RequestPasswordRecovery_Should_RaisePasswordRecoveryDomainEvent()
    {
        // Arrange
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        // Act
        user.RequestPasswordRecovery(TimeSpan.FromMinutes(30));

        // Assert
        var domainEvent = AssertDomainEventWasPublished<UserPasswordRecoveryDomainEvent>(user);
        domainEvent.UserId.Should().Be(user.Id);
        domainEvent.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ResetPassword_Should_SetPasswordResetAt()
    {
        // Arrange
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        user.RequestPasswordRecovery(TimeSpan.FromMinutes(30));

        // Act
        user.ResetPassword();

        // Assert
        user.PasswordResetAt.Should().NotBeNull();
    }

    [Fact]
    public void ResetPassword_Should_InvalidatePasswordResetToken()
    {
        // Arrange
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        user.RequestPasswordRecovery(TimeSpan.FromMinutes(30));

        // Act
        user.ResetPassword();

        // Assert
        user.PasswordResetToken.Should().BeNull();
    }

    [Fact]
    public void ResetPassword_Should_RaisePasswordResetDomainEvent()
    {
        // Arrange
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        user.RequestPasswordRecovery(TimeSpan.FromMinutes(30));

        // Act
        user.ResetPassword();

        // Assert
        var domainEvent = AssertDomainEventWasPublished<UserPasswordResetDomainEvent>(user);
        domainEvent.UserId.Should().Be(user.Id);
    }

    [Fact]
    public void RequestEmailChange_Should_CreateEmailChangeToken()
    {
        // Arrange
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        var newEmail = new Email("new@test.com");

        // Act
        user.RequestEmailChange(newEmail, TimeSpan.FromMinutes(30));

        // Assert
        user.EmailChangeToken.Should().NotBeNull();
        user.EmailChangeToken!.PendingEmail.Should().Be(newEmail);
        user.EmailChangeToken.ExpirationUtc.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void RequestEmailChange_Should_RaiseEmailChangeInitiatedDomainEvent()
    {
        // Arrange
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        var newEmail = new Email("new@test.com");

        // Act
        user.RequestEmailChange(newEmail, TimeSpan.FromMinutes(30));

        // Assert
        var domainEvent = AssertDomainEventWasPublished<UserEmailChangeInitiatedDomainEvent>(user);
        domainEvent.UserId.Should().Be(user.Id);
        domainEvent.NewEmail.Should().Be(newEmail.Value);
        domainEvent.CurrentEmail.Should().Be(UserData.Email.Value);
    }

    [Fact]
    public void ConfirmEmailChange_Should_NullifyEmailChangeToken()
    {
        // Arrange
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        var newEmail = new Email("new@test.com");
        user.RequestEmailChange(newEmail, TimeSpan.FromMinutes(30));

        // Act
        user.ConfirmEmailChange();

        // Assert
        user.EmailChangeToken.Should().BeNull();
    }

    [Fact]
    public void ConfirmEmailChange_Should_UpdateEmail_WhenTokenIsValid()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email, UserData.DateOfBirth);
        var newEmail = new Email("new@test.com");
        user.RequestEmailChange(newEmail, TimeSpan.FromMinutes(30));

        // Act
        user.ConfirmEmailChange();

        // Assert
        user.Email.Should().Be(newEmail);
        user.EmailChangeToken.Should().BeNull();
        user.EmailChangedAt.Should().NotBeNull();

        var domainEvent = AssertDomainEventWasPublished<UserEmailChangedDomainEvent>(user);
        domainEvent.Should().NotBeNull();
        domainEvent!.NewEmail.Should().Be(newEmail.Value);
    }

    [Fact]
    public void ConfirmEmailChange_Should_RaiseEventWithOldAndNewEmail()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email, UserData.DateOfBirth);
        var originalEmail = UserData.Email;
        var newEmail = new Email("new@test.com");
        user.RequestEmailChange(newEmail, TimeSpan.FromMinutes(30));

        // Act
        user.ConfirmEmailChange();

        // Assert
        var domainEvent = AssertDomainEventWasPublished<UserEmailChangedDomainEvent>(user);
        domainEvent.NewEmail.Should().Be(newEmail.Value);
        domainEvent.OldEmail.Should().Be(originalEmail.Value);
    }

    [Fact]
    public void UpdateProfile_Should_UpdateAllProperties()
    {
        // Arrange
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        var newFirstName = new FirstName("NewFirst");
        var newLastName = new LastName("NewLast");
        var newPhoneNumber = UserData.PhoneNumber;
        var newDateOfBirth = DateOfBirth.Create(new DateOnly(1990, 1, 1));

        // Act
        user.UpdateProfile(newFirstName, newLastName, newPhoneNumber, newDateOfBirth);

        // Assert
        user.FirstName.Should().Be(newFirstName);
        user.LastName.Should().Be(newLastName);
        user.PhoneNumber.Should().Be(newPhoneNumber);
        user.DateOfBirth.Should().Be(newDateOfBirth);
    }

    [Fact]
    public void UpdateProfile_Should_SetLastModifiedOn()
    {
        // Arrange
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        var newFirstName = new FirstName("NewFirst");
        var newLastName = new LastName("NewLast");
        var newPhoneNumber = UserData.PhoneNumber;
        var newDateOfBirth = DateOfBirth.Create(new DateOnly(1990, 1, 1));

        // Act
        user.UpdateProfile(newFirstName, newLastName, newPhoneNumber, newDateOfBirth);

        // Assert
        user.LastModifiedOn.Should().NotBeNull();
    }

    [Fact]
    public void UpdateProfile_Should_RaiseUserProfileUpdatedDomainEvent()
    {
        // Arrange
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        var newFirstName = new FirstName("NewFirst");
        var newLastName = new LastName("NewLast");
        var newPhoneNumber = UserData.PhoneNumber;
        var newDateOfBirth = DateOfBirth.Create(new DateOnly(1990, 1, 1));

        // Act
        user.UpdateProfile(newFirstName, newLastName, newPhoneNumber, newDateOfBirth);

        // Assert
        var domainEvent = AssertDomainEventWasPublished<UserProfileUpdatedDomainEvent>(user);
        domainEvent.UserId.Should().Be(user.Id);
    }

    [Fact]
    public void UpdateProfile_Should_AllowNullPhoneNumber()
    {
        // Arrange
        var user = User.Create(
            UserData.FirstName,
            UserData.LastName,
            UserData.Email,
            UserData.DateOfBirth);

        var newFirstName = new FirstName("NewFirst");
        var newLastName = new LastName("NewLast");
        PhoneNumber? newPhoneNumber = null;
        var newDateOfBirth = DateOfBirth.Create(new DateOnly(1990, 1, 1));

        // Act
        user.UpdateProfile(newFirstName, newLastName, newPhoneNumber, newDateOfBirth);

        // Assert
        user.PhoneNumber.Should().BeNull();
    }
}
