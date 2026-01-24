using FluentValidation;

namespace Bookify.Application.Extensions;

internal static class PasswordValidationExtensions
{
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(
        this IRuleBuilder<T, string> ruleBuilder, int minLength = 8) =>
        ruleBuilder
            .MinimumLength(minLength)
            .WithMessage($"The password must be at least {minLength} characters long.")
            .Matches(@"[A-Z]")
            .WithMessage("Must contain at least one uppercase letter.")
            .Matches(@"[a-z]")
            .WithMessage("Must contain at least one lowercase letter.")
            .Matches(@"\d")
            .WithMessage("Must contain at least one number.")
            .Matches(@"[^\da-zA-Z]")
            .WithMessage("Must contain at least one special character.");
}
