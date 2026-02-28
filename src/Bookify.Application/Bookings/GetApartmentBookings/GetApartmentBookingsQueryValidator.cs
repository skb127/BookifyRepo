using Bookify.Application.Common;
using FluentValidation;

namespace Bookify.Application.Bookings.GetApartmentBookings;

internal sealed class GetApartmentBookingsQueryValidator : PagedQueryValidator<GetApartmentBookingsQuery>
{
    public GetApartmentBookingsQueryValidator() =>
        RuleFor(q => q.Status)
            .InclusiveBetween(1, 5)
            .When(q => q.Status.HasValue);
}
