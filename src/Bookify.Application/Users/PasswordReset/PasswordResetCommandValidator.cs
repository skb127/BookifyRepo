using Bookify.Application.Extensions;
using FluentValidation;

namespace Bookify.Application.Users.PasswordReset;

internal sealed class PasswordResetCommandValidator : AbstractValidator<PasswordResetCommand>
{
    public PasswordResetCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty();
        
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("The new password is required.")
            .StrongPassword();
    }
}
