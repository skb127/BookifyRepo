using System.Collections.Concurrent;
using System.Diagnostics;
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
        int timeoutMs = 10_000,
        DateTime? since = null)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            EmailMessage? email = GetEmailTo(recipientEmail, subject, since);

            if (email is not null)
            {
                return email;
            }

            await Task.Delay(200).ConfigureAwait(false);
        }

        string subjectFilter = subject is not null ? $" with subject '{subject}'" : "";
        string sinceFilter = since is not null ? $" since {since:O}" : "";
        throw new TimeoutException(
            $"No email was sent to '{recipientEmail}'{subjectFilter}{sinceFilter} within {timeoutMs}ms. " +
            $"Emails sent: {Count}. " +
            "Verify that the Outbox background job is running.");
    }

    /// <summary>
    /// Polls for <paramref name="durationMs"/> ms and throws if an email to the specified address is received.
    /// Use this for negative assertions (verifying that no email is sent).
    /// </summary>
    public async Task EnsureNoEmailToAsync(string recipientEmail, int durationMs = 2_000, DateTime? since = null)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < durationMs)
        {
            EmailMessage? email = GetEmailTo(recipientEmail, since: since);

            if (email is not null)
            {
                throw new InvalidOperationException(
                    $"Unexpected email was sent to '{recipientEmail}'. Subject: '{email.Subject}'.");
            }

            await Task.Delay(200).ConfigureAwait(false);
        }
    }
}
