using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;

namespace Bookify.Application.Apartments.DeleteApartment;

internal sealed class DeleteApartmentCommandHandler : ICommandHandler<DeleteApartmentCommand>
{
    private readonly IApartmentRepository _apartmentRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DeleteApartmentCommandHandler(
        IApartmentRepository apartmentRepository,
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
    {
        _apartmentRepository = apartmentRepository;
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> Handle(DeleteApartmentCommand request, CancellationToken cancellationToken)
    {
        Apartment? apartment = await _apartmentRepository.GetByIdAsync(request.ApartmentId, cancellationToken);

        if (apartment is null)
        {
            return Result.Failure(ApartmentErrors.NotFound);
        }

        bool hasActiveBookings = await _bookingRepository.HasActiveBookingsAsync(apartment.Id, cancellationToken);
        if (hasActiveBookings)
        {
            return Result.Failure(ApartmentErrors.HasActiveBookings);
        }

        apartment.Delete(_dateTimeProvider.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
