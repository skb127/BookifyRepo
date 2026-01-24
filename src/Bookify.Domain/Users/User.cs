using Bookify.Domain.Abstractions;
using Bookify.Domain.Users.Events;

namespace Bookify.Domain.Users;
public sealed class User : Entity
{
    private readonly List<Role> _roles = [];

    private User(Guid id, FirstName firstName, LastName lastName, Email email, UserStatus status)
        : base(id)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Status = status;
    }

    /// <summary>
    /// Initializes a new instance of the User class. This constructor is intended for internal use and prevents
    /// external instantiation.
    /// </summary>
    private User()
    {
        
    }

    public FirstName FirstName { get; private set; } = null!;
    public LastName LastName { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public string IdentityId { get; private set; } = "";
    public UserStatus Status { get; private set; } = UserStatus.None;
    public DateTime? PasswordChangedAt { get; private set; }
    #pragma warning disable S1144
    public DateTime? DeletedAt { get; private set; }
    #pragma warning restore S1144
    public IReadOnlyCollection<Role> Roles => _roles.AsReadOnly();

    public static User Create(FirstName firstName, LastName lastName, Email email)
    {
        var user = new User(Guid.CreateVersion7(), firstName, lastName, email, UserStatus.Active);

        user.RaiseDomainEvent(new UserCreatedDomainEvent(user.Id));

        user._roles.Add(Role.Registered);

        return user;
    }

    public void SetIdentityId(string identityId) => 
        IdentityId = identityId;

    public void ChangePassword()
    {
        PasswordChangedAt = DateTime.UtcNow;
        
        RaiseDomainEvent(new ChangeUserPasswordDomainEvent(Id));
    }
}
