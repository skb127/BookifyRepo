using FluentValidation;

namespace Bookify.Application.Reviews.AddReview;

internal sealed class AddReviewCommandValidator : AbstractValidator<AddReviewCommand>
{
    public AddReviewCommandValidator()
    {
        RuleFor(c => c.BookingId).NotEmpty();
        RuleFor(c => c.Rating).InclusiveBetween(1, 5);
        RuleFor(c => c.Comment).NotEmpty().MaximumLength(200);
    }
}
