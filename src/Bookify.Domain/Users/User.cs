using Bookify.Domain.Abstractions;
using Bookify.Domain.Shared;
using Bookify.Domain.Users.Events;

namespace Bookify.Domain.Users;

public sealed class User : Entity
{
    private readonly List<Role> _roles = [];

    private User(Guid id, FirstName firstName, LastName lastName, Email email, DateOfBirth dateOfBirth, UserStatus status)
        : base(id)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        DateOfBirth = dateOfBirth;
        Status = status;
    }

    /// Initializes a new instance of the User class. This constructor is intended for internal use and prevents
    /// external instantiation.
    private User()
    {

    }

    public FirstName FirstName { get; private set; } = null!;
    public LastName LastName { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public DateOfBirth DateOfBirth { get; private set; } = null!;
    public PhoneNumber? PhoneNumber { get; private set; }
    public string IdentityId { get; private set; } = "";
    public UserStatus Status { get; private set; } = UserStatus.None;
    public DateTime? LastModifiedOn { get; private set; }
#pragma warning disable S1144
    public DateTime? DeletedAt { get; private set; }
#pragma warning restore S1144
    public DateTime? LastRecoveryRequestAt { get; private set; }
    public DateTime? PasswordChangedAt { get; private set; }
    public DateTime? PasswordResetAt { get; private set; }
    public IReadOnlyCollection<Role> Roles => _roles.AsReadOnly();
    public PasswordResetToken? PasswordResetToken { get; private set; }
    public EmailChangeToken? EmailChangeToken { get; private set; }
    public DateTime? EmailChangedAt { get; private set; }
    public string? StripeCustomerId { get; private set; }
    public int BanCount { get; private set; }
    public DateTime? DeletionScheduledAt { get; private set; }
    public AccountDeletionToken? AccountDeletionToken { get; private set; }

    public static User Create(FirstName firstName, LastName lastName, Email email, DateOfBirth dateOfBirth, Role userRole)
    {
        var user = new User(Guid.CreateVersion7(), firstName, lastName, email, dateOfBirth, UserStatus.Active);

        user.RaiseDomainEvent(new UserCreatedDomainEvent(user.Id));

        if (userRole == Role.Host)
        {
            user._roles.Add(Role.Guest);
            user._roles.Add(Role.Host);
        }
        else
        {
            user._roles.Add(userRole);
        }

        return user;
    }

    public void SetIdentityId(string identityId) =>

        IdentityId = identityId;

    public void SetPhoneNumber(PhoneNumber phoneNumber) =>
        PhoneNumber = phoneNumber;

    public void SetStripeCustomerId(string customerId) =>
        StripeCustomerId = customerId;

    public void ChangePassword()
    {
        PasswordChangedAt = DateTime.UtcNow;

        RaiseDomainEvent(new UserChangedPasswordDomainEvent(Id));
    }


    public void RequestPasswordRecovery(TimeSpan expiration)
    {
        LastRecoveryRequestAt = DateTime.UtcNow;

        // Token
        string rawToken = SecurityUtils.GenerateSecureToken();

        // Token hash
        string tokenHash = SecurityUtils.ComputeSha256Hash(rawToken);

        PasswordResetToken = PasswordResetToken.Create(Id, tokenHash, DateTimeOffset.UtcNow.Add(expiration));

        RaiseDomainEvent(new UserPasswordRecoveryDomainEvent(Id, rawToken));
    }


    public void ResetPassword()
    {
        PasswordResetAt = DateTime.UtcNow;

        // Invalidate the token after a successful password reset to prevent reuse.
        PasswordResetToken = null;

        RaiseDomainEvent(new UserPasswordResetDomainEvent(Id));
    }

    public void RequestEmailChange(Email newEmail, TimeSpan expiration)
    {
        // Generate token
        string rawToken = SecurityUtils.GenerateSecureToken();

        // Token hash
        string tokenHash = SecurityUtils.ComputeSha256Hash(rawToken);

        EmailChangeToken = EmailChangeToken.Create(Id, newEmail, tokenHash, DateTimeOffset.UtcNow.Add(expiration));

        RaiseDomainEvent(new UserEmailChangeInitiatedDomainEvent(Id, rawToken, newEmail.Value, Email.Value));
    }


    public void ConfirmEmailChange()
    {
        string oldEmail = Email.Value;
        Email = EmailChangeToken!.PendingEmail;
        EmailChangedAt = DateTime.UtcNow;
        EmailChangeToken = null;

        RaiseDomainEvent(new UserEmailChangedDomainEvent(Id, oldEmail, Email.Value));
    }

    public void UpdateProfile(FirstName firstName, LastName lastName, PhoneNumber? phoneNumber, DateOfBirth dateOfBirth)
    {
        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
        DateOfBirth = dateOfBirth;
        LastModifiedOn = DateTime.UtcNow;

        RaiseDomainEvent(new UserProfileUpdatedDomainEvent(Id));
    }

    public void RequestDeletion(string rawToken, string tokenHash, int gracePeriodHours)
    {
        AccountDeletionToken = AccountDeletionToken.Create(Id, tokenHash, DateTimeOffset.UtcNow.AddHours(gracePeriodHours));
        Status = UserStatus.PendingDeletion;
        DeletionScheduledAt = DateTime.UtcNow.AddHours(gracePeriodHours);

        RaiseDomainEvent(new UserAccountDeletionRequestedDomainEvent(Id, rawToken, DeletionScheduledAt.Value));
    }

    public void CancelDeletion()
    {
        Status = UserStatus.Active;
        DeletionScheduledAt = null;
        AccountDeletionToken = null;

        RaiseDomainEvent(new UserAccountDeletionCancelledDomainEvent(Id));
    }

    public void Delete()
    {
        Status = UserStatus.Deleted;
        DeletedAt = DateTime.UtcNow;
        AccountDeletionToken = null;

        RaiseDomainEvent(new UserDeletedDomainEvent(Id, StripeCustomerId));
    }

    public void Ban()
    {
        Status = UserStatus.Suspended;
        BanCount++;

        RaiseDomainEvent(new UserBannedDomainEvent(Id, IdentityId));
    }

    public void Unban()
    {
        Status = UserStatus.Active;

        RaiseDomainEvent(new UserUnbannedDomainEvent(Id, IdentityId));
    }
}
