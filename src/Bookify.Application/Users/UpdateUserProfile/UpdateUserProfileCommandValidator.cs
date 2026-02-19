using Bookify.Application.Extensions;
using FluentValidation;

namespace Bookify.Application.Users.UpdateUserProfile;

internal sealed class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileCommandValidator()
    {
        RuleFor(c => c.FirstName)
            .NotEmpty();

        RuleFor(c => c.LastName)
            .NotEmpty();

        RuleFor(c => c.DateOfBirth)
            .NotEmpty().WithMessage("Date of birth is required.")
            .MustBeAtLeast18YearsOld();

        RuleFor(c => c.PhoneNumber)
            .MustBePhoneNumber()
            .When(c => !string.IsNullOrWhiteSpace(c.PhoneNumber));
    }
}
