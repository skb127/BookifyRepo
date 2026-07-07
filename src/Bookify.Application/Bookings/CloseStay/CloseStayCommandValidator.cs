using FluentValidation;

namespace Bookify.Application.Bookings.CloseStay;

internal sealed class CloseStayCommandValidator : AbstractValidator<CloseStayCommand>
{
    public CloseStayCommandValidator()
    {
        RuleFor(c => c.BookingId).NotEmpty();

        RuleFor(c => c.CheckInDate)
            .NotEmpty()
            .WithMessage("Check-in date is required.");

        RuleFor(c => c.CheckOutDate)
            .NotEmpty()
            .WithMessage("Check-out date is required.");

        RuleFor(c => c.CheckInDate)
            .LessThanOrEqualTo(c => c.CheckOutDate)
            .WithMessage("Check-in date must be less than or equal to check-out date.")
            .When(c => c.CheckInDate != default && c.CheckOutDate != default);
    }
}
