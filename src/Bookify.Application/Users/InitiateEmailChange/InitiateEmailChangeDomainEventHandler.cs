using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Options;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using MediatR;
using Microsoft.Extensions.Options;

namespace Bookify.Application.Users.InitiateEmailChange;

internal sealed class InitiateEmailChangeDomainEventHandler : INotificationHandler<UserEmailChangeInitiatedDomainEvent>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly BookifyAppOptions _appOptions;

    public InitiateEmailChangeDomainEventHandler(
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

    public async Task Handle(UserEmailChangeInitiatedDomainEvent notification,
        CancellationToken cancellationToken)
    {
        User? user = await _userRepository.GetByIdAsync(notification.UserId, cancellationToken);

        if (user is null)
        {
            return;
        }

        // Send verification link to the NEW email
        await SendVerificationEmailAsync(user, notification, cancellationToken);

        // Send security alert to the CURRENT email
        await SendSecurityAlertEmailAsync(user, notification, cancellationToken);
    }

    private async Task SendVerificationEmailAsync(User user,
        UserEmailChangeInitiatedDomainEvent notification,
        CancellationToken cancellationToken)
    {
        var confirmEmailUri = new Uri(_appOptions.FrontendUrl, $"/users/confirm-email-change/{notification.Token}");
        string newEmailAddress = notification.NewEmail;

        var model = new
        {
            FirstName = user.FirstName.Value,
            ConfirmEmailUrl = confirmEmailUri.AbsoluteUri,
            NewEmail = newEmailAddress,
            ExpirationMinutes = 30
        };

        string emailBody = await _emailTemplateService.GenerateEmailBodyAsync(
            "EmailChangeVerification.html",
            model, cancellationToken);

        var emailMessage = new EmailMessage(
            notification.NewEmail,
            "Confirm Your New Email Address",
            emailBody);

        await _emailService.SendAsync(emailMessage, cancellationToken);
    }

    private async Task SendSecurityAlertEmailAsync(User user,
        UserEmailChangeInitiatedDomainEvent notification,
        CancellationToken cancellationToken)
    {
        string newEmailAddress = notification.NewEmail;

        var model = new
        {
            FirstName = user.FirstName.Value,
            NewEmail = newEmailAddress,
        };

        string emailBody = await _emailTemplateService.GenerateEmailBodyAsync(
            "EmailChangeAlert.html",
            model, cancellationToken);

        var emailMessage = new EmailMessage(
            notification.CurrentEmail,
            "Security Alert: Email Change Requested",
            emailBody);

        await _emailService.SendAsync(emailMessage, cancellationToken);
    }
}
