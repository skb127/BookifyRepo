using Bookify.Application.Apartments.CreateApartment;
using Bookify.Domain.Shared;
using FluentValidation.TestHelper;

namespace Bookify.Application.UnitTests.Apartments;

public class CreateApartmentCommandValidatorTests
{
    private readonly CreateApartmentCommandValidator _validator;

    public CreateApartmentCommandValidatorTests()
    {
        _validator = new CreateApartmentCommandValidator();
    }

    [Fact]
    public void Validate_ShouldNotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new CreateApartmentCommand(
            "Valid Name",
            "Valid Description",
            "Spain",
            "Madrid",
            "28001",
            "Madrid",
            "Calle Gran Vía 10",
            100.0m,
            Currency.Eur.Code,
            50.0m,
            Currency.Eur.Code,
            [1, 2, 3]);

        // Act & Assert
        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenNameIsEmpty()
    {
        // Arrange
        var command = new CreateApartmentCommand(
            "",
            "Valid Description",
            "Spain",
            "Madrid",
            "28001",
            "Madrid",
            "Calle Gran Vía 10",
            100.0m,
            Currency.Eur.Code,
            50.0m,
            Currency.Eur.Code,
            [1, 2, 3]);

        // Act & Assert
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenPriceAmountIsZeroOrNegative()
    {
        // Arrange
        var command = new CreateApartmentCommand(
            "Valid Name",
            "Valid Description",
            "Spain",
            "Madrid",
            "28001",
            "Madrid",
            "Calle Gran Vía 10",
            0m,
            Currency.Eur.Code,
            50.0m,
            Currency.Eur.Code,
            [1, 2, 3]);

        // Act & Assert
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.PriceAmount);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenCurrencyIsInvalid()
    {
        // Arrange
        var command = new CreateApartmentCommand(
            "Valid Name",
            "Valid Description",
            "Spain",
            "Madrid",
            "28001",
            "Madrid",
            "Calle Gran Vía 10",
            100.0m,
            "XYZ",
            50.0m,
            Currency.Eur.Code,
            [1, 2, 3]);

        // Act & Assert
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.PriceCurrency);
    }
}
