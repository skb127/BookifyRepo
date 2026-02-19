using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Options;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using MediatR;
using Microsoft.Extensions.Options;

namespace Bookify.Application.Users.PasswordRecovery;

internal sealed class PasswordRecoveryDomainEventHandler : INotificationHandler<UserPasswordRecoveryDomainEvent>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly BookifyAppOptions _appOptions;

    public PasswordRecoveryDomainEventHandler(
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

    public async Task Handle(UserPasswordRecoveryDomainEvent notification,
        CancellationToken cancellationToken)
    {
        User? user = await _userRepository.GetByIdAsync(notification.UserId, cancellationToken);

        if (user is null)
        {
            return;
        }

        var resetPasswordUri = new Uri(_appOptions.FrontendUrl, $"/users/reset-password/{notification.Token}");

        var model = new
        {
            FirstName = user.FirstName.Value,
            ResetPasswordUrl = resetPasswordUri.AbsoluteUri,
            ExpirationMinutes = 10
        };

        string emailBody = await _emailTemplateService.GenerateEmailBodyAsync(
            "PasswordRecoveryRequest.html",
            model, cancellationToken);

        var emailMessage = new EmailMessage(
            user.Email.Value,
            "Password Recovery Request",
            emailBody);

        await _emailService.SendAsync(emailMessage, cancellationToken);
    }
}

