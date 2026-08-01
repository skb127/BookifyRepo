using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using MediatR;

namespace Bookify.Application.Users.CancelAccountDeletion;

internal sealed class UserAccountDeletionCancelledDomainEventHandler : INotificationHandler<UserAccountDeletionCancelledDomainEvent>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;

    public UserAccountDeletionCancelledDomainEventHandler(
        IUserRepository userRepository,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService)
    {
        _userRepository = userRepository;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
    }

    public async Task Handle(UserAccountDeletionCancelledDomainEvent notification, CancellationToken cancellationToken)
    {
        User? user = await _userRepository.GetByIdAsync(notification.UserId, cancellationToken);

        if (user is null)
        {
            return;
        }

        var model = new
        {
            FirstName = user.FirstName.Value
        };

        string emailBody = await _emailTemplateService.GenerateEmailBodyAsync(
            "AccountDeletionCancelled.html",
            model,
            cancellationToken);

        var emailMessage = new EmailMessage(
            user.Email.Value,
            "Account Deactivation Cancelled",
            emailBody);

        await _emailService.SendAsync(emailMessage, cancellationToken);
    }
}
