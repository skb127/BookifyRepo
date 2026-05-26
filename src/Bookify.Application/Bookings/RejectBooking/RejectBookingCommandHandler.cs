using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Authorization;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Users;

namespace Bookify.Application.Bookings.RejectBooking;

internal sealed class RejectBookingCommandHandler : ICommandHandler<RejectBookingCommand>
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserContext _userContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly IApartmentRepository _apartmentRepository;

    public RejectBookingCommandHandler(
        IDateTimeProvider dateTimeProvider,
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        IAuthorizationService authorizationService,
        IApartmentRepository apartmentRepository)
    {
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _userContext = userContext;
        _authorizationService = authorizationService;
        _apartmentRepository = apartmentRepository;
    }

    public async Task<Result> Handle(
        RejectBookingCommand request,
        CancellationToken cancellationToken)
    {
        Booking? booking = await _bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure(BookingErrors.NotFound);
        }

        HashSet<string> permissions = await _authorizationService.GetPermissionsForUserAsync(_userContext.IdentityId);

        // Check if the user is an Admin with bookings:write permission
        bool hasBookingsWritePermission = permissions.Contains(Permission.BookingsWrite.Name);

        if (!hasBookingsWritePermission)
        {
            Apartment? apartment = await _apartmentRepository.GetByIdAsync(booking.ApartmentId, cancellationToken);
            if (apartment is null)
            {
                return Result.Failure(ApartmentErrors.NotFound);
            }

            if (apartment.OwnerId != _userContext.UserId)
            {
                return Result.Failure(BookingErrors.Unauthorized);
            }
        }

        BookingReason? reason = null;
        if (request.ReasonType.HasValue && request.ReasonType.Value != ReasonType.None)
        {
            reason = BookingReason.Create(request.ReasonType.Value, request.ReasonDescription, _dateTimeProvider.UtcNow);
        }

        Result result = booking.Reject(_dateTimeProvider.UtcNow, reason);

        if (result.IsFailure)
        {
            return result;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
