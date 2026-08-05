using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Options;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using MediatR;
using Microsoft.Extensions.Options;

namespace Bookify.Application.Users.RequestAccountDeletion;

internal sealed class UserAccountDeletionRequestedDomainEventHandler : INotificationHandler<UserAccountDeletionRequestedDomainEvent>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly BookifyAppOptions _appOptions;

    public UserAccountDeletionRequestedDomainEventHandler(
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

    public async Task Handle(UserAccountDeletionRequestedDomainEvent notification, CancellationToken cancellationToken)
    {
        User? user = await _userRepository.GetByIdAsync(notification.UserId, cancellationToken);

        if (user is null)
        {
            return;
        }

        var cancelDeactivationUri = new Uri(_appOptions.FrontendUrl, $"/cancel-deactivation?token={notification.RawToken}");

        var model = new
        {
            FirstName = user.FirstName.Value,
            CancelDeactivationUrl = cancelDeactivationUri.AbsoluteUri,
            ScheduledDateUtc = notification.DeletionScheduledAt.ToString("yyyy-MM-dd HH:mm:ss UTC", System.Globalization.CultureInfo.InvariantCulture)
        };

        string emailBody = await _emailTemplateService.GenerateEmailBodyAsync(
            "AccountDeletionRequested.html",
            model,
            cancellationToken);

        var emailMessage = new EmailMessage(
            user.Email.Value,
            "Account Deactivation Request",
            emailBody);

        await _emailService.SendAsync(emailMessage, cancellationToken);
    }
}
