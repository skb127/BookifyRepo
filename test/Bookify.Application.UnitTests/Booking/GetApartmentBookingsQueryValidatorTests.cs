using Bookify.Application.Bookings.GetApartmentBookings;
using FluentValidation.TestHelper;

namespace Bookify.Application.UnitTests.Booking;

public class GetApartmentBookingsQueryValidatorTests
{
    private readonly GetApartmentBookingsQueryValidator _validator = new();

    [Fact]
    public void Page_ShouldFail_WhenLessThanOne()
    {
        // Arrange
        var query = new GetApartmentBookingsQuery(Guid.NewGuid(), null, null, null, Page: 0, PageSize: 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.Page);
    }

    [Fact]
    public void PageSize_ShouldFail_WhenGreaterThan100()
    {
        // Arrange
        var query = new GetApartmentBookingsQuery(Guid.NewGuid(), null, null, null, Page: 1, PageSize: 101);

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.PageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Status_ShouldFail_WhenOutOfRange(int status)
    {
        // Arrange
        var query = new GetApartmentBookingsQuery(Guid.NewGuid(), Status: status, null, null, Page: 1, PageSize: 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.Status);
    }

    [Fact]
    public void Status_ShouldSucceed_WhenNull()
    {
        // Arrange
        var query = new GetApartmentBookingsQuery(Guid.NewGuid(), Status: null, null, null, Page: 1, PageSize: 20);

        // Act & Assert
        _validator.TestValidate(query).ShouldNotHaveValidationErrorFor(q => q.Status);
    }

    [Fact]
    public void Query_ShouldSucceed_WhenAllFieldsAreValid()
    {
        // Arrange
        var query = new GetApartmentBookingsQuery(
            ApartmentId: Guid.NewGuid(),
            Status: 4,
            StartDate: new DateOnly(2026, 5, 1),
            EndDate: new DateOnly(2026, 5, 5),
            Page: 5,
            PageSize: 50);

        // Act & Assert
        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }
}
