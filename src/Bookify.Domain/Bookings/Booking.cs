using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings.Events;
using Bookify.Domain.CancellationPolicies;
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
        Money extraGuestCharge,
        Money totalPrice,
        BookingStatus status,
        DateTime createdOnUtc,
        int guestCount)
        : base(id)
    {
        ApartmentId = apartmentId;
        UserId = userId;
        Duration = duration;
        PriceForPeriod = priceForPeriod;
        CleaningFee = cleaningFee;
        AmenitiesUpCharge = amenitiesUpCharge;
        ExtraGuestCharge = extraGuestCharge;
        TotalPrice = totalPrice;
        Status = status;
        CreatedOnUtc = createdOnUtc;
        GuestCount = guestCount;
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
    public Money ExtraGuestCharge { get; private set; } = null!;
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
    public int GuestCount { get; private set; }

    private readonly List<BookingReason> _reasons = [];
    public IReadOnlyList<BookingReason> Reasons => _reasons.AsReadOnly();

    private readonly List<BookingTax> _taxes = [];
    public IReadOnlyList<BookingTax> Taxes => _taxes.AsReadOnly();

    public void AddTax(BookingTax tax) => _taxes.Add(tax);

    // Factory Method
    public static Booking Reserve(
        Apartment apartment,
        Guid userId,
        DateRange duration,
        DateTime utcNow,
        PricingService pricingService,
        int guestCount = 1)
    {
        PricingDetails pricingDetails = pricingService.CalculatePrice(apartment, duration, guestCount);

        var booking = new Booking(
            Guid.CreateVersion7(),
            apartment.Id,
            userId,
            duration,
            pricingDetails.PriceForPeriod,
            pricingDetails.CleaningFee,
            pricingDetails.AmenitiesUpCharge,
            pricingDetails.ExtraGuestCharge,
            pricingDetails.TotalPrice,
            BookingStatus.PendingPayment,
            utcNow,
            guestCount);

        booking.RaiseDomainEvent(new BookingReservedDomainEvent(booking.Id));

        apartment.LastBookedOnUtc = utcNow;

        return booking;
    }

    public Result AuthorizePayment(string stripeSessionId, string? stripePaymentIntentId)
    {
        if (Status != BookingStatus.PendingPayment)
        {
            return Result.Failure(BookingErrors.NotPendingPayment);
        }

        Status = BookingStatus.Reserved;
        PaymentStatus = PaymentStatus.Authorized;

        RaiseDomainEvent(new BookingPaymentAuthorizedDomainEvent(Id, stripeSessionId, stripePaymentIntentId));

        return Result.Success();
    }

    public Result MarkAsPaid(string stripePaymentIntentId, DateTime utcNow)
    {
        if (Status != BookingStatus.PendingPayment)
        {
            return Result.Failure(BookingErrors.NotPendingPayment);
        }

        Status = BookingStatus.Confirmed;
        PaymentStatus = PaymentStatus.Paid;
        ConfirmedOnUtc = utcNow;
        ExpiresAt = null;

        RaiseDomainEvent(new BookingPaymentCompletedDomainEvent(Id, stripePaymentIntentId));

        return Result.Success();
    }

    public Result InitiateRefund(decimal refundAmount, string currency, string reason)
    {
        if (Status != BookingStatus.Cancelled || PaymentStatus != PaymentStatus.Paid)
        {
            return Result.Failure(BookingErrors.RefundNotEligible);
        }

        PaymentStatus = PaymentStatus.RefundProcessing;

        RaiseDomainEvent(new BookingRefundInitiatedDomainEvent(Id, refundAmount, currency, reason));

        return Result.Success();
    }

    public Result Confirm(DateTime utcNow)
    {
        if (Status != BookingStatus.Reserved)
        {
            return Result.Failure(BookingErrors.NotReserved);
        }

        Status = BookingStatus.Confirmed;
        ConfirmedOnUtc = utcNow;
        ExpiresAt = null;

        if (PaymentStatus == PaymentStatus.Authorized)
        {
            PaymentStatus = PaymentStatus.Paid;
        }

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
        ExpiresAt = null;

        if (reason is not null)
        {
            _reasons.Add(reason);
        }

        if (PaymentStatus == PaymentStatus.Authorized)
        {
            PaymentStatus = PaymentStatus.AuthorizationReleased;
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

    public Result<PenaltyResult> Cancel(
        DateTime utcNow,
        CancellationPolicy? policy,
        CancellationPolicyEngine? policyEngine,
        bool cancelledByHost,
        BookingReason? reason = null)
    {
        if (Status != BookingStatus.Reserved && Status != BookingStatus.Confirmed &&
            Status != BookingStatus.PendingPayment || cancelledByHost && Status == BookingStatus.PendingPayment)
        {
            return Result.Failure<PenaltyResult>(BookingErrors.NotCancellable);
        }

        if (Status == BookingStatus.Confirmed)
        {
            var currentDate = DateOnly.FromDateTime(utcNow);

            if (currentDate > Duration.Start)
            {
                return Result.Failure<PenaltyResult>(BookingErrors.AlreadyStarted);
            }
        }

        var penaltyResult = new PenaltyResult(0m, 0m, 0m, TotalPrice.Currency.Code, false);

        switch (PaymentStatus)
        {
            case PaymentStatus.Paid when policy is null || policyEngine is null:
                return Result.Failure<PenaltyResult>(BookingErrors.NoPolicyAvailable);
            case PaymentStatus.Paid:
                penaltyResult = policyEngine.CalculatePenalty(this, policy, utcNow, cancelledByHost);
                break;
            case PaymentStatus.Authorized:
                PaymentStatus = PaymentStatus.AuthorizationReleased;
                break;
        }

        Status = BookingStatus.Cancelled;
        CancelledOnUtc = utcNow;
        ExpiresAt = null;

        if (reason is not null)
        {
            _reasons.Add(reason);
        }

        RaiseDomainEvent(new BookingCancelledDomainEvent(
            Id,
            penaltyResult.RefundAmount > 0 ? penaltyResult.RefundAmount : null,
            cancelledByHost,
            penaltyResult.Currency,
            penaltyResult.HostPenaltyAmount));

        return Result.Success(penaltyResult);
    }

    public Result CheckIn(DateTime utcNow, DateOnly? guestCheckInDate = null)
    {
        if (Status != BookingStatus.Confirmed)
        {
            return Result.Failure(BookingErrors.NotConfirmed);
        }

        var today = DateOnly.FromDateTime(utcNow);

        if (today < Duration.Start)
        {
            return Result.Failure(BookingErrors.CheckInTooEarly);
        }

        if (today > Duration.End)
        {
            return Result.Failure(BookingErrors.StayNotYetEnded); // Using StayNotYetEnded here to signify the stay is already over
        }

        if (guestCheckInDate.HasValue && (guestCheckInDate.Value > today || guestCheckInDate.Value < Duration.Start))
        {
            return Result.Failure(BookingErrors.InvalidCheckInDate);
        }

        Status = BookingStatus.InProgress;
        
        if (guestCheckInDate.HasValue)
        {
            Duration = DateRange.Create(guestCheckInDate.Value, Duration.End);
        }

        CheckedInOnUtc = guestCheckInDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) 
                         ?? utcNow;

        RaiseDomainEvent(new BookingCheckedInDomainEvent(Id));

        return Result.Success();
    }

    public Result CheckOut(DateTime utcNow, BookingReason? reason = null, DateOnly? guestCheckOutDate = null)
    {
        if (Status != BookingStatus.InProgress)
        {
            return Result.Failure(BookingErrors.NotInProgress);
        }

        var today = DateOnly.FromDateTime(utcNow);

        if (guestCheckOutDate.HasValue && (guestCheckOutDate.Value > today || guestCheckOutDate.Value < Duration.Start))
        {
            return Result.Failure(BookingErrors.InvalidCheckOutDate);
        }

        Status = BookingStatus.Completed;
        
        if (guestCheckOutDate.HasValue)
        {
            Duration = DateRange.Create(Duration.Start, guestCheckOutDate.Value);
        }

        CompletedOnUtc = guestCheckOutDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                         ?? utcNow;

        if (reason is not null)
        {
            _reasons.Add(reason);
        }

        RaiseDomainEvent(new BookingCheckedOutDomainEvent(Id));

        return Result.Success();
    }

    public Result CloseStay(DateTime utcNow, DateOnly checkInDate, DateOnly checkOutDate)
    {
        if (Status != BookingStatus.Confirmed)
        {
            return Result.Failure(BookingErrors.NotConfirmed);
        }

        var today = DateOnly.FromDateTime(utcNow);

        if (today <= Duration.End)
        {
            return Result.Failure(BookingErrors.StayNotYetEnded);
        }

        if (checkInDate < Duration.Start || checkInDate > today)
        {
            return Result.Failure(BookingErrors.InvalidCheckInDate);
        }

        if (checkOutDate < checkInDate || checkOutDate > today)
        {
            return Result.Failure(BookingErrors.InvalidCheckOutDate);
        }

        Status = BookingStatus.Completed;
        CheckedInOnUtc = checkInDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        CompletedOnUtc = checkOutDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        RaiseDomainEvent(new BookingClosedStayDomainEvent(Id));

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
        if (Status != BookingStatus.Reserved && Status != BookingStatus.PendingPayment)
        {
            return Result.Failure(BookingErrors.NotExpirable);
        }

        Status = BookingStatus.Expired;
        ExpiredOnUtc = utcNow;
        ExpiresAt = null;

        if (PaymentStatus == PaymentStatus.Authorized)
        {
            PaymentStatus = PaymentStatus.AuthorizationReleased;
        }

        RaiseDomainEvent(new BookingExpiredDomainEvent(Id));

        return Result.Success();
    }

    public Result CompleteRefund(DateTime utcNow)
    {
        _ = utcNow;

        if (PaymentStatus != PaymentStatus.RefundProcessing)
        {
            return Result.Failure(BookingErrors.RefundNotEligible);
        }

        PaymentStatus = PaymentStatus.Refunded;

        return Result.Success();
    }

    public Result RevertRefundFailure(DateTime utcNow)
    {
        _ = utcNow;

        if (PaymentStatus != PaymentStatus.RefundProcessing)
        {
            return Result.Failure(BookingErrors.RefundNotEligible);
        }

        PaymentStatus = PaymentStatus.Paid;

        return Result.Success();
    }

    public void MarkCompletionNotified(DateTime utcNow) =>
        CompletedNotificationSentAt = utcNow;

    public void SetExpiresAt(DateTime? expiresAt) =>
        ExpiresAt = expiresAt;
}
