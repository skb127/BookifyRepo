using Bookify.Application.TaxRules.UpdateTaxRule;
using FluentAssertions;
using FluentValidation.Results;

namespace Bookify.Application.UnitTests.TaxRules;

public class UpdateTaxRuleCommandValidatorTests
{
    private readonly UpdateTaxRuleCommandValidator _validator = new();

    [Fact]
    public void Validator_ShouldFail_WhenIdIsEmpty()
    {
        var command = new UpdateTaxRuleCommand(
            Guid.Empty,
            "ES",
            "Region",
            "City",
            0.10m,
            1,
            "VAT",
            new DateOnly(2026, 1, 1),
            null);

        ValidationResult result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateTaxRuleCommand.Id));
    }

    [Fact]
    public void Validator_ShouldFail_WhenCountryCodeIsEmpty()
    {
        var command = new UpdateTaxRuleCommand(
            Guid.NewGuid(),
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
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateTaxRuleCommand.CountryCode));
    }

    [Fact]
    public void Validator_ShouldFail_WhenCountryCodeLengthIsNotTwo()
    {
        var command = new UpdateTaxRuleCommand(
            Guid.NewGuid(),
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
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateTaxRuleCommand.CountryCode));
    }

    [Fact]
    public void Validator_ShouldFail_WhenNameIsEmpty()
    {
        var command = new UpdateTaxRuleCommand(
            Guid.NewGuid(),
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
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateTaxRuleCommand.Name));
    }

    [Fact]
    public void Validator_ShouldFail_WhenRateValueIsNegative()
    {
        var command = new UpdateTaxRuleCommand(
            Guid.NewGuid(),
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
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateTaxRuleCommand.RateValue));
    }

    [Fact]
    public void Validator_ShouldFail_WhenRateTypeIsInvalid()
    {
        var command = new UpdateTaxRuleCommand(
            Guid.NewGuid(),
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
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateTaxRuleCommand.RateType));
    }

    [Fact]
    public void Validator_ShouldFail_WhenEffectiveToIsLessThanEffectiveFrom()
    {
        var command = new UpdateTaxRuleCommand(
            Guid.NewGuid(),
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
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateTaxRuleCommand.EffectiveTo));
    }

    [Fact]
    public void Validator_ShouldSucceed_WhenCommandIsValid()
    {
        var command = new UpdateTaxRuleCommand(
            Guid.NewGuid(),
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
