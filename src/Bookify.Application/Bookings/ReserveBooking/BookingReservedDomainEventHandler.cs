using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Scheduling;
using Bookify.Application.Options;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using MediatR;
using Microsoft.Extensions.Options;

namespace Bookify.Application.Bookings.ReserveBooking;

internal sealed class BookingReservedDomainEventHandler : INotificationHandler<BookingReservedDomainEvent>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IJobScheduler _jobScheduler;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly BookingOptions _bookingOptions;

    public BookingReservedDomainEventHandler(
        IBookingRepository bookingRepository,
        IJobScheduler jobScheduler,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        IOptions<BookingOptions> bookingOptions)
    {
        _bookingRepository = bookingRepository;
        _jobScheduler = jobScheduler;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _bookingOptions = bookingOptions.Value;
    }

    public async Task Handle(BookingReservedDomainEvent notification, CancellationToken cancellationToken)
    {
        Booking? booking = await _bookingRepository.GetByIdAsync(notification.BookingId, cancellationToken);

        if (booking is null)
        {
            return;
        }

        DateTime utcNow = _dateTimeProvider.UtcNow;
        DateTime expiresAt = utcNow.AddMinutes(_bookingOptions.CheckoutSessionTtlMinutes);

        booking.SetExpiresAt(expiresAt);

        await _jobScheduler.ScheduleExpireCheckoutSessionAsync(booking.Id, expiresAt, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
