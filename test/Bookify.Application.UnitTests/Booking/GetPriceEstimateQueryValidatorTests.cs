using Bookify.Application.Bookings.GetPriceEstimate;
using FluentValidation.TestHelper;

namespace Bookify.Application.UnitTests.Booking;

public class GetPriceEstimateQueryValidatorTests
{
    private readonly GetPriceEstimateQueryValidator _validator = new();

    [Fact]
    public void ApartmentId_ShouldFail_WhenEmpty()
    {
        // Arrange
        var query = new GetPriceEstimateQuery(Guid.Empty, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 5));

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.ApartmentId);
    }

    [Fact]
    public void StartDate_ShouldFail_WhenEmpty()
    {
        // Arrange
        var query = new GetPriceEstimateQuery(Guid.NewGuid(), default, new DateOnly(2025, 1, 5));

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.StartDate);
    }

    [Fact]
    public void EndDate_ShouldFail_WhenEmpty()
    {
        // Arrange
        var query = new GetPriceEstimateQuery(Guid.NewGuid(), new DateOnly(2025, 1, 1), default);

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.EndDate);
    }

    [Fact]
    public void EndDate_ShouldFail_WhenBeforeStartDate()
    {
        // Arrange
        var query = new GetPriceEstimateQuery(Guid.NewGuid(), new DateOnly(2025, 1, 5), new DateOnly(2025, 1, 1));

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.EndDate);
    }

    [Fact]
    public void EndDate_ShouldFail_WhenSameAsStartDate()
    {
        // Arrange
        var query = new GetPriceEstimateQuery(Guid.NewGuid(), new DateOnly(2025, 1, 5), new DateOnly(2025, 1, 5));

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.EndDate);
    }

    [Fact]
    public void Query_ShouldSucceed_WhenAllFieldsAreValid()
    {
        // Arrange
        var query = new GetPriceEstimateQuery(Guid.NewGuid(), new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 5));

        // Act & Assert
        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }
}
