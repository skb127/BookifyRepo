using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Bookify.Infrastructure.Email;

internal sealed class SmtpEmailService : IEmailService
{
    private readonly ISmtpClient _smtpClient;
    private readonly EmailOptions _emailOptions;

    public SmtpEmailService(IOptions<EmailOptions> emailOptions,

        ISmtpClient smtpClient)
    {
        _smtpClient = smtpClient;
        _emailOptions = emailOptions.Value;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        using var mimeMessage = new MimeMessage();
        string senderEmail = message.From ?? _emailOptions.SenderEmail;
        string senderName = message.FromName ?? _emailOptions.SenderName;
        mimeMessage.From.Add(new MailboxAddress(senderName, senderEmail));
        mimeMessage.To.Add(new MailboxAddress("", message.To));
        mimeMessage.Subject = message.Subject;

        var builder = new BodyBuilder();

        if (message.IsHtml)
        {
            builder.HtmlBody = message.Body;
        }
        else
        {
            builder.TextBody = message.Body;
        }

        if (message.Attachments.Count > 0)
        {
            foreach (EmailAttachment attachment in message.Attachments)
            {
                using var stream = new MemoryStream(attachment.Content);
                await builder.Attachments.AddAsync(attachment.FileName, stream, ContentType.Parse(attachment.ContentType), cancellationToken);
            }
        }

        mimeMessage.Body = builder.ToMessageBody();

        try
        {
            await _smtpClient.ConnectAsync(_emailOptions.Host, _emailOptions.Port, SecureSocketOptions.StartTls, cancellationToken);
            await _smtpClient.AuthenticateAsync(_emailOptions.Username, _emailOptions.Password, cancellationToken);
            await _smtpClient.SendAsync(mimeMessage, cancellationToken);
        }
        finally
        {
            await _smtpClient.DisconnectAsync(true, cancellationToken);
        }
    }
}
