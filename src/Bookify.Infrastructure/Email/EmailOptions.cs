namespace Bookify.Infrastructure.Email;

public sealed class EmailOptions
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
    public string SenderEmail { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string SupportEmail { get; init; } = string.Empty;
}