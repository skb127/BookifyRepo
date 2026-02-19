namespace Bookify.Domain.Users;

public record DateOfBirth
{
    private DateOfBirth(DateOnly value) => Value = value;

    public DateOnly Value { get; init; }

    public static DateOfBirth Create(DateOnly value)
    {
        if (value > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ArgumentException("Date of birth cannot be in the future", nameof(value));
        }

        return new DateOfBirth(value);
    }
}
