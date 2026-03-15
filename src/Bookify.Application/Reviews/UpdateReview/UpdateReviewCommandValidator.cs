using FluentValidation;

namespace Bookify.Application.Reviews.UpdateReview;

internal sealed class UpdateReviewCommandValidator : AbstractValidator<UpdateReviewCommand>
{
    public UpdateReviewCommandValidator()
    {
        RuleFor(c => c.ReviewId).NotEmpty();

        RuleFor(c => c.Rating)
            .InclusiveBetween(1, 5)
            .WithMessage("Rating must be between 1 and 5.");

        RuleFor(c => c.Comment)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Comment must not exceed 200 characters.");
    }
}
