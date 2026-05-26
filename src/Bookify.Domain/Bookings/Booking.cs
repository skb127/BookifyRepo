using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings.Events;
using Bookify.Domain.Shared;

namespace Bookify.Domain.Bookings;

public sealed class Booking : Entity
{
    private Booking(
        Guid id,
        Guid apartmentId,
        Guid userId,
        DateRange duration,
        Money priceForPeriod,
        Money cleaningFee,
        Money amenitiesUpCharge,
        Money totalPrice,
        BookingStatus status,
        DateTime createdOnUtc)
        : base(id)
    {
        ApartmentId = apartmentId;
        UserId = userId;
        Duration = duration;
        PriceForPeriod = priceForPeriod;
        CleaningFee = cleaningFee;
        AmenitiesUpCharge = amenitiesUpCharge;
        TotalPrice = totalPrice;
        Status = status;
        CreatedOnUtc = createdOnUtc;
    }

    /// <summary>
    /// Initializes a new instance of the Booking class. This constructor is intended for internal use and prevents
    /// external instantiation.
    /// </summary>
    private Booking()
    {
    }

    public Guid ApartmentId { get; private set; }
    public Guid UserId { get; private set; }
    public DateRange Duration { get; private set; } = null!;
    public Money PriceForPeriod { get; private set; } = null!;
    public Money CleaningFee { get; private set; } = null!;
    public Money AmenitiesUpCharge { get; private set; } = null!;
    public Money TotalPrice { get; private set; } = null!;
    public BookingStatus Status { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? ConfirmedOnUtc { get; private set; }
    public DateTime? RejectedOnUtc { get; private set; }
    public DateTime? CompletedOnUtc { get; private set; }
    public DateTime? CompletedNotificationSentAt { get; private set; }
    public DateTime? CancelledOnUtc { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; } = PaymentStatus.Unpaid;
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? CheckedInOnUtc { get; private set; }
    public DateTime? NoShowAt { get; private set; }
    public DateTime? ExpiredOnUtc { get; private set; }

    private readonly List<BookingReason> _reasons = [];
    public IReadOnlyList<BookingReason> Reasons => _reasons.AsReadOnly();

    // Factory Method
    public static Booking Reserve(
        Apartment apartment,
        Guid userId,
        DateRange duration,
        DateTime utcNow,
        PricingService pricingService,
        bool instantBooking = false,
        int courtesyBlockHours = 24)
    {
        PricingDetails pricingDetails = pricingService.CalculatePrice(apartment, duration);

        var booking = new Booking(
            Guid.CreateVersion7(),
            apartment.Id,
            userId,
            duration,
            pricingDetails.PriceForPeriod,
            pricingDetails.CleaningFee,
            pricingDetails.AmenitiesUpCharge,
            pricingDetails.TotalPrice,
            instantBooking ? BookingStatus.Confirmed : BookingStatus.Reserved,
            utcNow);

        if (instantBooking)
        {
            booking.ConfirmedOnUtc = utcNow;
            booking.RaiseDomainEvent(new BookingConfirmedDomainEvent(booking.Id));
        }
        else
        {
            booking.ExpiresAt = utcNow.AddHours(courtesyBlockHours);
            booking.RaiseDomainEvent(new BookingReservedDomainEvent(booking.Id));
        }

        apartment.LastBookedOnUtc = utcNow;

        return booking;
    }

    public Result Confirm(DateTime utcNow)
    {
        if (Status != BookingStatus.Reserved)
        {
            return Result.Failure(BookingErrors.NotReserved);
        }

        Status = BookingStatus.Confirmed;
        ConfirmedOnUtc = utcNow;

        RaiseDomainEvent(new BookingConfirmedDomainEvent(Id));

        return Result.Success();
    }

    public Result Reject(DateTime utcNow, BookingReason? reason = null)
    {
        if (Status != BookingStatus.Reserved)
        {
            return Result.Failure(BookingErrors.NotReserved);
        }

        Status = BookingStatus.Rejected;
        RejectedOnUtc = utcNow;

        if (reason is not null)
        {
            _reasons.Add(reason);
        }

        RaiseDomainEvent(new BookingRejectedDomainEvent(Id));

        return Result.Success();
    }

    public Result Complete(DateTime utcNow)
    {
        if (Status != BookingStatus.InProgress)
        {
            return Result.Failure(BookingErrors.NotInProgress);
        }

        Status = BookingStatus.Completed;
        CompletedOnUtc = utcNow;

        RaiseDomainEvent(new BookingCompletedDomainEvent(Id));

        return Result.Success();
    }

    public Result Cancel(DateTime utcNow, BookingReason? reason = null)
    {
        if (Status != BookingStatus.Reserved && Status != BookingStatus.Confirmed)
        {
            return Result.Failure(BookingErrors.NotConfirmed);
        }

        if (Status == BookingStatus.Confirmed)
        {
            var currentDate = DateOnly.FromDateTime(utcNow);

            if (currentDate > Duration.Start)
            {
                return Result.Failure(BookingErrors.AlreadyStarted);
            }
        }

        Status = BookingStatus.Cancelled;
        CancelledOnUtc = utcNow;

        if (reason is not null)
        {
            _reasons.Add(reason);
        }

        RaiseDomainEvent(new BookingCancelledDomainEvent(Id));

        return Result.Success();
    }

    public Result CheckIn(DateTime utcNow)
    {
        if (Status != BookingStatus.Confirmed)
        {
            return Result.Failure(BookingErrors.NotConfirmed);
        }

        Status = BookingStatus.InProgress;
        CheckedInOnUtc = utcNow;

        RaiseDomainEvent(new BookingCheckedInDomainEvent(Id));

        return Result.Success();
    }

    public Result CheckOut(DateTime utcNow, BookingReason? reason = null)
    {
        if (Status != BookingStatus.InProgress)
        {
            return Result.Failure(BookingErrors.NotInProgress);
        }

        Status = BookingStatus.Completed;
        CompletedOnUtc = utcNow;

        if (reason is not null)
        {
            _reasons.Add(reason);
        }

        RaiseDomainEvent(new BookingCheckedOutDomainEvent(Id));

        return Result.Success();
    }

    public Result MarkNoShow(DateTime utcNow)
    {
        if (Status != BookingStatus.Confirmed)
        {
            return Result.Failure(BookingErrors.NotConfirmed);
        }

        if (DateOnly.FromDateTime(utcNow) <= Duration.Start)
        {
            return Result.Failure(BookingErrors.CannotMarkNoShow);
        }

        Status = BookingStatus.NoShow;
        NoShowAt = utcNow;

        RaiseDomainEvent(new BookingNoShowDomainEvent(Id));

        return Result.Success();
    }

    public Result Expire(DateTime utcNow)
    {
        if (Status != BookingStatus.Reserved)
        {
            return Result.Failure(BookingErrors.NotReserved);
        }

        Status = BookingStatus.Expired;
        ExpiredOnUtc = utcNow;

        RaiseDomainEvent(new BookingExpiredDomainEvent(Id));

        return Result.Success();
    }

    public void MarkCompletionNotified(DateTime utcNow) =>
        CompletedNotificationSentAt = utcNow;
}
