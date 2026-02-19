using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using MediatR;

namespace Bookify.Application.Users.ConfirmEmailChange;

internal sealed class ConfirmEmailChangedDomainEventHandler : INotificationHandler<UserEmailChangedDomainEvent>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;

    public ConfirmEmailChangedDomainEventHandler(
        IUserRepository userRepository,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService)
    {
        _userRepository = userRepository;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
    }

    public async Task Handle(UserEmailChangedDomainEvent notification, CancellationToken cancellationToken)
    {
        User? user = await _userRepository.GetByIdAsync(notification.UserId, cancellationToken);

        if (user is null)
        {
            return;
        }

        // 1. Send notification to OLD email
        await SendEmailChangedNotificationAsync(user, notification, cancellationToken);

        // 2. Send notification to NEW email
        await SendEmailChangedSuccessNotificationAsync(user, notification, cancellationToken);
    }

    private async Task SendEmailChangedNotificationAsync(User user, UserEmailChangedDomainEvent notification, CancellationToken cancellationToken)
    {
        var model = new
        {
            user.FirstName.Value,
            notification.NewEmail
        };

        string emailBody = await _emailTemplateService.GenerateEmailBodyAsync(
            "EmailChanged.html",
            model,
            cancellationToken);

        var emailMessage = new EmailMessage(
            notification.OldEmail,
            "Security Alert: Email Address Changed",
            emailBody);

        await _emailService.SendAsync(emailMessage, cancellationToken);
    }

    private async Task SendEmailChangedSuccessNotificationAsync(User user, UserEmailChangedDomainEvent notification, CancellationToken cancellationToken)
    {
        var model = new
        {
            user.FirstName.Value,
            notification.NewEmail
        };

        string emailBody = await _emailTemplateService.GenerateEmailBodyAsync(
            "EmailChangedSuccess.html",
            model,
            cancellationToken);

        var emailMessage = new EmailMessage(
            notification.NewEmail,
            "Email Change Successful",
            emailBody);

        await _emailService.SendAsync(emailMessage, cancellationToken);
    }
}
