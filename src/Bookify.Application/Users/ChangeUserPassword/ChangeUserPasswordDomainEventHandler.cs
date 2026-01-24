using Bookify.Application.Abstractions.Email;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using MediatR;

namespace Bookify.Application.Users.ChangeUserPassword;

internal sealed class ChangeUserPasswordDomainEventHandler : INotificationHandler<ChangeUserPasswordDomainEvent>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    
    public ChangeUserPasswordDomainEventHandler(
        IUserRepository userRepository,
        IEmailService emailService)
    {
        _userRepository = userRepository;
        _emailService = emailService;
    }
    
    public async Task Handle(ChangeUserPasswordDomainEvent notification, 
        CancellationToken cancellationToken)
    {
        User? user = await _userRepository.GetByIdAsync(notification.UserId, cancellationToken);

        if (user is null)
        {
            return;
        }

        await _emailService.SendAsync(
            user.Email,
            "Password Changed",
            "Your password has been successfully changed.");
    }
}
