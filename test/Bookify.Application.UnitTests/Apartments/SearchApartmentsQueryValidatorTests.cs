using Bookify.Application.Apartments.SearchApartments;
using FluentValidation.TestHelper;

namespace Bookify.Application.UnitTests.Apartments;

public class SearchApartmentsQueryValidatorTests
{
    private readonly SearchApartmentsQueryValidator _validator;

    public SearchApartmentsQueryValidatorTests()
    {
        _validator = new SearchApartmentsQueryValidator();
    }

    #region Happy Path

    [Fact]
    public void Validate_ShouldNotHaveErrors_WhenNoFiltersProvided()
    {
        // Arrange - Base valid paginated query without filters
        var query = new SearchApartmentsQuery(
            null, null, null, null, null, null, null, null, 1, 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldNotHaveErrors_WhenAllFiltersAreValid()
    {
        // Arrange
        var query = new SearchApartmentsQuery(
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 10),
            "Madrid",
            "Spain",
            50m,
            150m,
            "EUR",
            [1, 2, 3],
            2,
            50);

        // Act & Assert
        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldNotHaveErrors_WhenOnlyDateRangeIsProvided()
    {
        // Arrange
        var query = new SearchApartmentsQuery(
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 10),
            null, null, null, null, null, null, 1, 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldNotHaveErrors_WhenStartDateEqualsEndDate()
    {
        // Arrange - Valid for a single-day occupancy search
        var query = new SearchApartmentsQuery(
            new DateOnly(2026, 5, 5),
            new DateOnly(2026, 5, 5),
            null, null, null, null, null, null, 1, 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region Dates

    [Fact]
    public void Validate_ShouldHaveError_WhenStartDateProvidedWithoutEndDate()
    {
        // Arrange
        var query = new SearchApartmentsQuery(
            new DateOnly(2026, 5, 1),
            null,
            null, null, null, null, null, null, 1, 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.EndDate);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenEndDateProvidedWithoutStartDate()
    {
        // Arrange
        var query = new SearchApartmentsQuery(
            null,
            new DateOnly(2026, 5, 10),
            null, null, null, null, null, null, 1, 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.StartDate);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenStartDateIsAfterEndDate()
    {
        // Arrange
        var query = new SearchApartmentsQuery(
            new DateOnly(2026, 5, 20),
            new DateOnly(2026, 5, 10),
            null, null, null, null, null, null, 1, 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.StartDate);
    }

    #endregion

    #region Prices

    [Fact]
    public void Validate_ShouldHaveError_WhenMinPriceIsNegative()
    {
        // Arrange
        var query = new SearchApartmentsQuery(
            null, null, null, null,
            -1m, null, null, null, 1, 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.MinPrice);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenMaxPriceIsNegative()
    {
        // Arrange
        var query = new SearchApartmentsQuery(
            null, null, null, null,
            null, -1m, null, null, 1, 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.MaxPrice);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenMinPriceIsGreaterThanMaxPrice()
    {
        // Arrange
        var query = new SearchApartmentsQuery(
            null, null, null, null,
            200m, 100m, null, null, 1, 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.MinPrice);
    }

    [Fact]
    public void Validate_ShouldNotHaveErrors_WhenMinPriceEqualsMaxPrice()
    {
        // Arrange - Valid exact price filter
        var query = new SearchApartmentsQuery(
            null, null, null, null,
            150m, 150m, null, null, 1, 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region Currency

    [Fact]
    public void Validate_ShouldHaveError_WhenCurrencyIsUnknown()
    {
        // Arrange
        var query = new SearchApartmentsQuery(
            null, null, null, null, null, null,
            "XYZ", null, 1, 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.Currency);
    }

    [Fact]
    public void Validate_ShouldNotHaveErrors_WhenCurrencyIsEur()
    {
        // Arrange
        var query = new SearchApartmentsQuery(
            null, null, null, null, null, null,
            "EUR", null, 1, 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region Pagination (Inherited Rules)

    [Fact]
    public void Validate_ShouldHaveError_WhenPageIsZero()
    {
        // Arrange
        var query = new SearchApartmentsQuery(
            null, null, null, null, null, null, null, null,
            0, 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.Page);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenPageSizeExceedsMaximum()
    {
        // Arrange
        var query = new SearchApartmentsQuery(
            null, null, null, null, null, null, null, null,
            1, 101); // Max allowed is 100 in PagedQueryValidator

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.PageSize);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenPageSizeIsZero()
    {
        // Arrange
        var query = new SearchApartmentsQuery(
            null, null, null, null, null, null, null, null,
            1, 0);

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.PageSize);
    }

    #endregion
}
