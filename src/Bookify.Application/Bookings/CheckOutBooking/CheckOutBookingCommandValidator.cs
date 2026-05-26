using FluentValidation;

namespace Bookify.Application.Bookings.CheckOutBooking;

internal sealed class CheckOutBookingCommandValidator : AbstractValidator<CheckOutBookingCommand>
{
    public CheckOutBookingCommandValidator()
    {
        RuleFor(c => c.BookingId).NotEmpty();
        RuleFor(c => c.ReasonDescription).MaximumLength(500);
    }
}
