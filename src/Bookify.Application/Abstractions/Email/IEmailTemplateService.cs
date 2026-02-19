namespace Bookify.Application.Abstractions.Email;

public interface IEmailTemplateService
{
    Task<string> GenerateEmailBodyAsync(string templateName, object model, CancellationToken cancellationToken = default);
}
