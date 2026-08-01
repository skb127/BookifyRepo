namespace Bookify.Application.Options;

public sealed class AccountDeletionOptions
{
    public int GracePeriodHours { get; set; } = 72;
}
