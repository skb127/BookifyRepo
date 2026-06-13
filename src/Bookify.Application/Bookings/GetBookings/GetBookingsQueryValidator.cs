using Bookify.Application.Common;
using FluentValidation;

namespace Bookify.Application.Bookings.GetBookings;

internal sealed class GetBookingsQueryValidator : PagedQueryValidator<GetBookingsQuery>
{
    public GetBookingsQueryValidator() =>
        RuleFor(q => q.Status)
            .InclusiveBetween(1, 9)
            .When(q => q.Status.HasValue);
}
