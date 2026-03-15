using Bookify.Application.Reviews.UpdateReview;
using FluentValidation.TestHelper;

namespace Bookify.Application.UnitTests.Reviews;

public class UpdateReviewCommandValidatorTests
{
    private readonly UpdateReviewCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldHaveError_WhenReviewIdIsEmpty()
    {
        // Arrange
        var command = new UpdateReviewCommand(Guid.Empty, 5, "Great place");

        // Act & Assert
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.ReviewId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Validate_ShouldHaveError_WhenRatingIsOutOfBounds(int invalidRating)
    {
        // Arrange
        var command = new UpdateReviewCommand(Guid.CreateVersion7(), invalidRating, "Great place");

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
        // Forcing invalid comment into string (non-nullable in record) to test validation rules
        var command = new UpdateReviewCommand(Guid.CreateVersion7(), 5, invalidComment!);

        // Act & Assert
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.Comment);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenCommentIsTooLong()
    {
        // Arrange
        var longComment = new string('A', 201);
        var command = new UpdateReviewCommand(Guid.CreateVersion7(), 5, longComment);

        // Act & Assert
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.Comment);
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenCommandIsValid()
    {
        // Arrange
        var command = new UpdateReviewCommand(Guid.CreateVersion7(), 5, "Great place, really enjoyed staying here.");

        // Act & Assert
        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }
}
