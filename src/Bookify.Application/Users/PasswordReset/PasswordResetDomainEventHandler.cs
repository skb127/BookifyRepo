using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Options;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using MediatR;
using Microsoft.Extensions.Options;

namespace Bookify.Application.Users.PasswordReset;

internal sealed class PasswordResetDomainEventHandler : INotificationHandler<UserPasswordResetDomainEvent>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly BookifyAppOptions _appOptions;

    public PasswordResetDomainEventHandler(
        IUserRepository userRepository,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        IOptions<BookifyAppOptions> appOptions)
    {
        _userRepository = userRepository;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _appOptions = appOptions.Value;
    }

    public async Task Handle(UserPasswordResetDomainEvent notification,
        CancellationToken cancellationToken)
    {
        User? user = await _userRepository.GetByIdAsync(notification.UserId, cancellationToken);

        if (user is null)
        {
            return;
        }

        var loginUri = new Uri(_appOptions.FrontendUrl, "/login");

        var model = new
        {
            FirstName = user.FirstName.Value,
            LoginUrl = loginUri.AbsoluteUri
        };

        string emailBody = await _emailTemplateService.GenerateEmailBodyAsync(
            "PasswordResetSuccess.html",
            model, cancellationToken);

        var emailMessage = new EmailMessage(
            user.Email.Value,
            "Password Reset Successful",
            emailBody);

        await _emailService.SendAsync(emailMessage, cancellationToken);
    }
}

