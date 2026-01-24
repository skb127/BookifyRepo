using Bookify.Application.Extensions;
using FluentValidation;

namespace Bookify.Application.Users.ChangeUserPassword;

internal class ChangeUserPasswordCommandValidator : AbstractValidator<ChangeUserPasswordCommand>
{
    public ChangeUserPasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("The current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("The new password is required.")
            .StrongPassword()
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("The new password can't be the same as the current one.");
    }
}
