using Bookify.Application.Extensions;
using FluentValidation;

namespace Bookify.Application.Users.RegisterGuest;

internal sealed class RegisterGuestCommandValidator : AbstractValidator<RegisterGuestCommand>
{
    public RegisterGuestCommandValidator()
    {
        RuleFor(c => c.FirstName).NotEmpty();

        RuleFor(c => c.LastName).NotEmpty();

        RuleFor(c => c.Email).EmailAddress();

        RuleFor(c => c.Password)
            .NotEmpty()
            .StrongPassword();

        RuleFor(c => c.DateOfBirth)
            .NotNull()
            .MustBeAtLeast18YearsOld();
    }
}
