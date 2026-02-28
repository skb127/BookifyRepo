using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;

namespace Bookify.Application.Apartments.CheckApartmentAvailability;

internal sealed class CheckApartmentAvailabilityQueryHandler : IQueryHandler<CheckApartmentAvailabilityQuery, ApartmentAvailabilityResponse>
{
    private readonly IApartmentRepository _apartmentRepository;
    private readonly IBookingRepository _bookingRepository;

    public CheckApartmentAvailabilityQueryHandler(
        IApartmentRepository apartmentRepository,
        IBookingRepository bookingRepository)
    {
        _apartmentRepository = apartmentRepository;
        _bookingRepository = bookingRepository;
    }

    public async Task<Result<ApartmentAvailabilityResponse>> Handle(
        CheckApartmentAvailabilityQuery request,
        CancellationToken cancellationToken)
    {
        Apartment? apartment = await _apartmentRepository.GetByIdAsync(request.ApartmentId, cancellationToken);

        if (apartment is null)
        {
            return Result.Failure<ApartmentAvailabilityResponse>(ApartmentErrors.NotFound);
        }

        var duration = DateRange.Create(request.StartDate, request.EndDate);

        bool isOverlapping = await _bookingRepository.IsOverlappingAsync(
            apartment,
            duration,
            cancellationToken);

        return new ApartmentAvailabilityResponse(!isOverlapping, request.StartDate, request.EndDate);
    }
}
