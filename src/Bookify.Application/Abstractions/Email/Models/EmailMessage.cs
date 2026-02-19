namespace Bookify.Application.Abstractions.Email.Models;

public sealed class EmailMessage
{
    private readonly List<EmailAttachment> _attachments = [];

    public string To { get; private set; }
    public string Subject { get; private set; }
    public string Body { get; private set; }
    public bool IsHtml { get; private set; }
    public string? From { get; private set; }
    public string? FromName { get; private set; }
    public IReadOnlyCollection<EmailAttachment> Attachments => _attachments.AsReadOnly();

    public EmailMessage(string to, string subject, string body, bool isHtml = true)
    {
        To = to;
        Subject = subject;
        Body = body;
        IsHtml = isHtml;
    }
    
    public EmailMessage WithFrom(string email, string? name = null)
    {
        From = email;
        FromName = name;
        return this;
    }

    public void AddAttachment(string fileName, byte[] content, string contentType) =>
        _attachments.Add(new EmailAttachment(fileName, content, contentType));
}
