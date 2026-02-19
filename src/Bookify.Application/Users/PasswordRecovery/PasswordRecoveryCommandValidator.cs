using FluentValidation;

namespace Bookify.Application.Users.PasswordRecovery;

internal sealed class PasswordRecoveryCommandValidator : AbstractValidator<PasswordRecoveryCommand>
{
    public PasswordRecoveryCommandValidator() =>
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
}
