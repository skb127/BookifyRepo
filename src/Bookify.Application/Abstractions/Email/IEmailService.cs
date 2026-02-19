using Bookify.Application.Abstractions.Email.Models;

namespace Bookify.Application.Abstractions.Email;
    
public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
