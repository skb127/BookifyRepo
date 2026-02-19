using Bookify.Application.Abstractions.Authentication;
using FluentValidation;

namespace Bookify.Application.Users.InitiateEmailChange;

internal sealed class InitiateEmailChangeCommandValidator : AbstractValidator<InitiateEmailChangeCommand>
{
    public InitiateEmailChangeCommandValidator(IUserContext userContext)
    {
        RuleFor(x => x.NewEmail)
            .NotEmpty()
            .WithMessage("New email is required")
            .EmailAddress()
            .WithMessage("Invalid email format")
            .Must(email => email != userContext.Email)
            .WithMessage("New email must be different from the current email");

        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("Current password is required");
    }
}
