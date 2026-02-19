using System.Text.RegularExpressions;
using FluentValidation;

namespace Bookify.Application.Extensions;

public static partial class UserValidationExtensions
{
    private const string PhoneNumberPattern = @"^\+?[1-9]\d{1,14}$"; // E.164

    public static IRuleBuilderOptions<T, string?> MustBePhoneNumber<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Matches(MyRegex())
            .WithMessage("Phone number format is invalid.");

    public static IRuleBuilderOptions<T, DateOnly> MustBeValidDateOfBirth<T>(this IRuleBuilder<T, DateOnly> ruleBuilder) =>
        ruleBuilder
            .Must(date => date <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth cannot be in the future.");

    public static IRuleBuilderOptions<T, DateOnly?> MustBeValidDateOfBirth<T>(this IRuleBuilder<T, DateOnly?> ruleBuilder) =>
        ruleBuilder
            .Must(date => !date.HasValue || date.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth cannot be in the future.");

    public static IRuleBuilderOptions<T, DateOnly> MustBeAtLeast18YearsOld<T>(this IRuleBuilder<T, DateOnly> ruleBuilder) =>
        ruleBuilder
           .Must(dob => dob <= DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-18)))
           .WithMessage("User must be at least 18 years old.");

    public static IRuleBuilderOptions<T, DateOnly?> MustBeAtLeast18YearsOld<T>(this IRuleBuilder<T, DateOnly?> ruleBuilder) =>
        ruleBuilder
           .Must(dob => !dob.HasValue || dob.Value <= DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-18)))
           .WithMessage("User must be at least 18 years old.");

    [GeneratedRegex(PhoneNumberPattern)]
    private static partial Regex MyRegex();
}
