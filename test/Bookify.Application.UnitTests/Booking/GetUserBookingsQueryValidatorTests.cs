using Bookify.Application.Bookings.GetUserBookings;
using FluentValidation.TestHelper;

namespace Bookify.Application.UnitTests.Booking;

public class GetUserBookingsQueryValidatorTests
{
    private readonly GetUserBookingsQueryValidator _validator = new();

    [Fact]
    public void Page_ShouldFail_WhenLessThanOne()
    {
        // Arrange
        var query = new GetUserBookingsQuery(null, null, null, Page: 0, PageSize: 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.Page);
    }

    [Fact]
    public void Page_ShouldSucceed_WhenGreaterThanOrEqualToOne()
    {
        // Arrange
        var query = new GetUserBookingsQuery(null, null, null, Page: 1, PageSize: 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldNotHaveValidationErrorFor(q => q.Page);
    }

    [Fact]
    public void PageSize_ShouldFail_WhenLessThanOne()
    {
        // Arrange
        var query = new GetUserBookingsQuery(null, null, null, Page: 1, PageSize: 0);

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.PageSize);
    }

    [Fact]
    public void PageSize_ShouldFail_WhenGreaterThan100()
    {
        // Arrange
        var query = new GetUserBookingsQuery(null, null, null, Page: 1, PageSize: 101);

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.PageSize);
    }

    [Fact]
    public void PageSize_ShouldSucceed_WhenInRange()
    {
        // Arrange
        var query = new GetUserBookingsQuery(null, null, null, Page: 1, PageSize: 50);

        // Act & Assert
        _validator.TestValidate(query).ShouldNotHaveValidationErrorFor(q => q.PageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(100)]
    public void Status_ShouldFail_WhenOutOfRange(int status)
    {
        // Arrange
        var query = new GetUserBookingsQuery(Status: status, null, null, Page: 1, PageSize: 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.Status);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Status_ShouldSucceed_WhenInRange(int status)
    {
        // Arrange
        var query = new GetUserBookingsQuery(Status: status, null, null, Page: 1, PageSize: 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldNotHaveValidationErrorFor(q => q.Status);
    }

    [Fact]
    public void Status_ShouldSucceed_WhenNull()
    {
        // Arrange
        var query = new GetUserBookingsQuery(Status: null, null, null, Page: 1, PageSize: 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldNotHaveValidationErrorFor(q => q.Status);
    }

    [Fact]
    public void Query_ShouldSucceed_WhenAllFieldsAreValid()
    {
        // Arrange
        var query = new GetUserBookingsQuery(
            Status: 2,
            StartDate: new DateOnly(2026, 1, 1),
            EndDate: new DateOnly(2026, 1, 10),
            Page: 2,
            PageSize: 15);

        // Act & Assert
        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }
}
