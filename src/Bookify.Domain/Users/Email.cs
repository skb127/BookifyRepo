namespace Bookify.Domain.Users;

public record Email
{
    public string Value { get; }
    public Email(string value) => Value = value.ToLowerInvariant();
}
