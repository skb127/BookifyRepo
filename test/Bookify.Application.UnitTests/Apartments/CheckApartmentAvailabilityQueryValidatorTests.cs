using Bookify.Application.Apartments.CheckApartmentAvailability;
using FluentValidation.TestHelper;

namespace Bookify.Application.UnitTests.Apartments;

public class CheckApartmentAvailabilityQueryValidatorTests
{
    private readonly CheckApartmentAvailabilityQueryValidator _validator = new();

    [Fact]
    public void Validate_ShouldNotHaveErrors_WhenQueryIsValid()
    {
        // Arrange
        var query = new CheckApartmentAvailabilityQuery(
            Guid.CreateVersion7(),
            new DateOnly(2026, 3, 1),
            new DateOnly(2026, 3, 10));

        // Act & Assert
        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenStartDateIsAfterEndDate()
    {
        // Arrange
        var query = new CheckApartmentAvailabilityQuery(
            Guid.CreateVersion7(),
            new DateOnly(2026, 3, 10),
            new DateOnly(2026, 3, 1));

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.StartDate);
    }

    [Fact]
    public void Validate_ShouldNotHaveErrors_WhenStartDateEqualsEndDate()
    {
        // Arrange
        var query = new CheckApartmentAvailabilityQuery(
            Guid.CreateVersion7(),
            new DateOnly(2026, 3, 5),
            new DateOnly(2026, 3, 5));

        // Act & Assert
        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }
}
