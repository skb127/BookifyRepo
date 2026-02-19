using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Options;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using MediatR;
using Microsoft.Extensions.Options;

namespace Bookify.Application.Users.RegisterUser;

internal sealed class RegisterUserDomainEventHandler : INotificationHandler<UserCreatedDomainEvent>
{
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IUserRepository _userRepository;
    private readonly BookifyAppOptions _appOptions;

    public RegisterUserDomainEventHandler(
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        IUserRepository userRepository,
        IOptions<BookifyAppOptions> appOptions)
    {
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _userRepository = userRepository;
        _appOptions = appOptions.Value;
    }

    public async Task Handle(UserCreatedDomainEvent notification, CancellationToken cancellationToken)
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
            Email = user.Email.Value,
            LoginUrl = loginUri.AbsoluteUri
        };

        string emailBody = await _emailTemplateService.GenerateEmailBodyAsync(
            "WelcomeUser.html",
            model, cancellationToken);

        var emailMessage = new EmailMessage(
            user.Email.Value,
            "Welcome to Bookify",
            emailBody);

        await _emailService.SendAsync(emailMessage, cancellationToken);
    }
}