using Bookify.Application.Extensions;
using FluentValidation;

namespace Bookify.Application.Users.RegisterHost;

internal sealed class RegisterHostCommandValidator : AbstractValidator<RegisterHostCommand>
{
    public RegisterHostCommandValidator()
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

        RuleFor(c => c.PhoneNumber)
            .NotEmpty()
            .MustBePhoneNumber();
    }
}
