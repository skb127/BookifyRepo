using Bookify.Application.TaxRules.CreateTaxRule;
using FluentAssertions;
using FluentValidation.Results;

namespace Bookify.Application.UnitTests.TaxRules;

public class CreateTaxRuleCommandValidatorTests
{
    private readonly CreateTaxRuleCommandValidator _validator = new();

    [Fact]
    public void Validator_ShouldFail_WhenCountryCodeIsEmpty()
    {
        var command = new CreateTaxRuleCommand(
            "",
            "Region",
            "City",
            0.10m,
            1,
            "VAT",
            new DateOnly(2026, 1, 1),
            null);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTaxRuleCommand.CountryCode));
    }

    [Fact]
    public void Validator_ShouldFail_WhenCountryCodeLengthIsNotTwo()
    {
        var command = new CreateTaxRuleCommand(
            "ESP",
            "Region",
            "City",
            0.10m,
            1,
            "VAT",
            new DateOnly(2026, 1, 1),
            null);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTaxRuleCommand.CountryCode));
    }

    [Fact]
    public void Validator_ShouldFail_WhenNameIsEmpty()
    {
        var command = new CreateTaxRuleCommand(
            "ES",
            "Region",
            "City",
            0.10m,
            1,
            "",
            new DateOnly(2026, 1, 1),
            null);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTaxRuleCommand.Name));
    }

    [Fact]
    public void Validator_ShouldFail_WhenRateValueIsNegative()
    {
        var command = new CreateTaxRuleCommand(
            "ES",
            "Region",
            "City",
            -0.05m,
            1,
            "VAT",
            new DateOnly(2026, 1, 1),
            null);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTaxRuleCommand.RateValue));
    }

    [Fact]
    public void Validator_ShouldFail_WhenRateTypeIsInvalid()
    {
        var command = new CreateTaxRuleCommand(
            "ES",
            "Region",
            "City",
            0.10m,
            5,
            "VAT",
            new DateOnly(2026, 1, 1),
            null);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTaxRuleCommand.RateType));
    }

    [Fact]
    public void Validator_ShouldFail_WhenEffectiveToIsLessThanEffectiveFrom()
    {
        var command = new CreateTaxRuleCommand(
            "ES",
            "Region",
            "City",
            0.10m,
            1,
            "VAT",
            new DateOnly(2026, 1, 10),
            new DateOnly(2026, 1, 5));

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateTaxRuleCommand.EffectiveTo));
    }

    [Fact]
    public void Validator_ShouldSucceed_WhenCommandIsValid()
    {
        var command = new CreateTaxRuleCommand(
            "ES",
            "Region",
            "City",
            0.10m,
            1,
            "VAT",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
