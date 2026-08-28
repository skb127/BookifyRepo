using System.Collections.Concurrent;
using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;

namespace Bookify.Application.IntegrationTests.Infrastructure;

/// <summary>
/// Mock implementation of IEmailService that captures sent emails for testing.
/// Stores a timestamp with each email so tests can filter out stale emails from previous tests.
/// </summary>
public class MockEmailService : IEmailService
{
    private readonly ConcurrentBag<(EmailMessage Message, DateTime ReceivedAt)> _sentEmails = [];

    public IReadOnlyCollection<EmailMessage> SentEmails =>
        [.. _sentEmails.Select(e => e.Message)];

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _sentEmails.Add((message, DateTime.UtcNow));
        return Task.CompletedTask;
    }

    public void Clear() =>
        _sentEmails.Clear();

    public bool HasEmailTo(string email, DateTime? since = null) =>
        _sentEmails.Any(e =>
            e.Message.To.Equals(email, StringComparison.OrdinalIgnoreCase) &&
            (since == null || e.ReceivedAt >= since));

    public EmailMessage? GetEmailTo(string email, string? subject = null, DateTime? since = null) =>
        _sentEmails
            .Where(e => since == null || e.ReceivedAt >= since)
            .Select(e => e.Message)
            .FirstOrDefault(e =>
                e.To.Equals(email, StringComparison.OrdinalIgnoreCase) &&
                (subject == null || e.Subject.Equals(subject, StringComparison.OrdinalIgnoreCase)));

    public int Count => _sentEmails.Count;

    /// <summary>
    /// Polls the service until an email to the specified address (and optionally subject) appears, or times out.
    /// Use <paramref name="since"/> to ignore emails received before a given point in time.
    /// </summary>
    public async Task<EmailMessage> WaitForEmailToAsync(
        string recipientEmail,
        string? subject = null,
        int timeoutMs = 25_000,
        DateTime? since = null)
    {
        string subjectFilter = subject is not null ? $" with subject '{subject}'" : "";
        string sinceFilter = since is not null ? $" since {since:O}" : "";
        string failureMessage =
            $"No email was sent to '{recipientEmail}'{subjectFilter}{sinceFilter} within {timeoutMs}ms. " +
            $"Emails sent: {Count}. " +
            "Verify that the Outbox background job is running.";

        EmailMessage? email = await PollingHelper.WaitUntilAsync(
            action: () => Task.FromResult(GetEmailTo(recipientEmail, subject, since)),
            isReady: e => e is not null,
            timeout: TimeSpan.FromMilliseconds(timeoutMs),
            interval: TimeSpan.FromMilliseconds(200),
            failureMessage: failureMessage).ConfigureAwait(false);

        return email!;
    }

    /// <summary>
    /// Polls for <paramref name="durationMs"/> ms and throws if an email to the specified address (and optionally subject) is received.
    /// Use this for negative assertions (verifying that no email is sent).
    /// </summary>
    public async Task EnsureNoEmailToAsync(
        string recipientEmail,
        string? subject = null,
        int durationMs = 2_000,
        DateTime? since = null)
    {
        string subjectFilter = subject is not null ? $" with subject '{subject}'" : "";
        await PollingHelper.EnsureNeverAsync(
            action: () => Task.FromResult(GetEmailTo(recipientEmail, subject, since)),
            isUnexpected: e => e is not null,
            duration: TimeSpan.FromMilliseconds(durationMs),
            interval: TimeSpan.FromMilliseconds(200),
            failureMessage: e => $"Unexpected email{subjectFilter} was sent to '{recipientEmail}'. Subject: '{e?.Subject}'.").ConfigureAwait(false);
    }
}
