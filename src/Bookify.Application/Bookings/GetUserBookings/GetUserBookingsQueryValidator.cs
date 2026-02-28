using Bookify.Application.Common;
using FluentValidation;

namespace Bookify.Application.Bookings.GetUserBookings;

internal sealed class GetUserBookingsQueryValidator : PagedQueryValidator<GetUserBookingsQuery>
{
    public GetUserBookingsQueryValidator() =>
        RuleFor(q => q.Status)
            .InclusiveBetween(1, 5)
            .When(q => q.Status.HasValue);
}
