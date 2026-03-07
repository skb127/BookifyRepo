using Bookify.Application.Apartments.UpdateApartment;
using Bookify.Domain.Shared;
using FluentValidation.TestHelper;

namespace Bookify.Application.UnitTests.Apartments;

public class UpdateApartmentCommandValidatorTests
{
    private static readonly UpdateApartmentCommand ValidCommand = new(
        Guid.NewGuid(),
        "Valid Name",
        "Valid Description",
        "Spain",
        "Madrid",
        "28001",
        "Madrid",
        "Gran Vía 12",
        175.0m,
        Currency.Eur.Code,
        35.0m,
        Currency.Eur.Code,
        [1, 2, 5]);

    private readonly UpdateApartmentCommandValidator _validator;

    public UpdateApartmentCommandValidatorTests()
    {
        _validator = new UpdateApartmentCommandValidator();
    }

    [Fact]
    public void Validate_ShouldNotHaveErrors_WhenCommandIsValid()
    {
        // Act & Assert
        _validator.TestValidate(ValidCommand).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenIdIsEmpty()
    {
        // Arrange
        UpdateApartmentCommand command = ValidCommand with { Id = Guid.Empty };

        // Act & Assert
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.Id);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenNameIsEmpty()
    {
        // Arrange
        UpdateApartmentCommand command = ValidCommand with { Name = "" };

        // Act & Assert
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenPriceAmountIsZeroOrNegative()
    {
        // Arrange
        UpdateApartmentCommand command = ValidCommand with { PriceAmount = 0m };

        // Act & Assert
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.PriceAmount);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenCurrencyIsInvalid()
    {
        // Arrange
        UpdateApartmentCommand command = ValidCommand with { PriceCurrency = "XYZ" };

        // Act & Assert
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.PriceCurrency);
    }
}
