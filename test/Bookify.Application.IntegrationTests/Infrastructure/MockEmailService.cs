using System.Collections.Concurrent;
using System.Diagnostics;
using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;

namespace Bookify.Application.IntegrationTests.Infrastructure;

/// <summary>
/// Mock implementation of IEmailService that captures sent emails for testing.
/// </summary>
public class MockEmailService : IEmailService
{
    private readonly ConcurrentBag<EmailMessage> _sentEmails = [];

    public IReadOnlyCollection<EmailMessage> SentEmails => [.. _sentEmails];

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _sentEmails.Add(message);
        return Task.CompletedTask;
    }

    public void Clear() => 
        _sentEmails.Clear();

    public bool HasEmailTo(string email) =>
        _sentEmails.Any(e => e.To.Equals(email, StringComparison.OrdinalIgnoreCase));

    public EmailMessage? GetEmailTo(string email) =>
        _sentEmails.FirstOrDefault(e => e.To.Equals(email, StringComparison.OrdinalIgnoreCase));

    public int Count => _sentEmails.Count;

    /// <summary>
    /// Polls the service until an email to the specified address appears, or times out.
    /// </summary>
    public async Task<EmailMessage> WaitForEmailToAsync(string recipientEmail, int timeoutMs = 10_000)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            EmailMessage? email = GetEmailTo(recipientEmail);

            if (email is not null)
            {
                return email;
            }

            await Task.Delay(200).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"No email was sent to '{recipientEmail}' within {timeoutMs}ms. " +
            $"Emails sent: {Count}. " +
            "Verify that the Outbox background job is running.");
    }
}
