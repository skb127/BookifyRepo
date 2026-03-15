using Bookify.Application.Reviews.AddReview;
using FluentValidation.TestHelper;

namespace Bookify.Application.UnitTests.Reviews;

public class AddReviewCommandValidatorTests
{
    private readonly AddReviewCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldHaveError_WhenBookingIdIsEmpty()
    {
        // Arrange
        var command = new AddReviewCommand(Guid.Empty, 5, "Great place");

        // Act & Assert
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.BookingId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Validate_ShouldHaveError_WhenRatingIsOutOfBounds(int invalidRating)
    {
        // Arrange
        var command = new AddReviewCommand(Guid.CreateVersion7(), invalidRating, "Great place");

        // Act & Assert
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.Rating);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Validate_ShouldHaveError_WhenCommentIsEmpty(string? invalidComment)
    {
        // Arrange
        // The record might complain dynamically if string is non-nullable but for validation testing we force it
        var command = new AddReviewCommand(Guid.CreateVersion7(), 5, invalidComment!);

        // Act & Assert
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.Comment);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenCommentIsTooLong()
    {
        // Arrange
        var longComment = new string('A', 201);
        var command = new AddReviewCommand(Guid.CreateVersion7(), 5, longComment);

        // Act & Assert
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.Comment);
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenCommandIsValid()
    {
        // Arrange
        var command = new AddReviewCommand(Guid.CreateVersion7(), 5, "Great place");

        // Act & Assert
        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }
}
