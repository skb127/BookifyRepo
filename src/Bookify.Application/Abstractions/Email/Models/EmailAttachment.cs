namespace Bookify.Application.Abstractions.Email.Models;

public record EmailAttachment(string FileName, byte[] Content, string ContentType);
