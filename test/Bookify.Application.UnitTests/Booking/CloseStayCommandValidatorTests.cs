using Bookify.Application.Bookings.CloseStay;
using FluentValidation.TestHelper;

namespace Bookify.Application.UnitTests.Booking;

public class CloseStayCommandValidatorTests
{
    private readonly CloseStayCommandValidator _validator;

    public CloseStayCommandValidatorTests()
    {
        _validator = new CloseStayCommandValidator();
    }

    [Fact]
    public void Validate_ShouldNotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new CloseStayCommand(
            Guid.NewGuid(),
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 1, 10));

        // Act & Assert
        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenBookingIdIsEmpty()
    {
        // Arrange
        var command = new CloseStayCommand(
            Guid.Empty,
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 1, 10));

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.BookingId);
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenCheckInDateIsEmpty()
    {
        // Arrange
        var command = new CloseStayCommand(
            Guid.NewGuid(),
            default, // 0001-01-01
            new DateOnly(2025, 1, 10));

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.CheckInDate)
            .WithErrorMessage("Check-in date is required.");
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenCheckOutDateIsEmpty()
    {
        // Arrange
        var command = new CloseStayCommand(
            Guid.NewGuid(),
            new DateOnly(2025, 1, 1),
            default); // 0001-01-01

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.CheckOutDate)
            .WithErrorMessage("Check-out date is required.");
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenCheckInDateIsAfterCheckOutDate()
    {
        // Arrange
        var command = new CloseStayCommand(
            Guid.NewGuid(),
            new DateOnly(2025, 1, 10),
            new DateOnly(2025, 1, 1));

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.CheckInDate)
            .WithErrorMessage("Check-in date must be less than or equal to check-out date.");
    }
}
