using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using Bookify.Domain.CancellationPolicies;
using Bookify.Domain.Shared;
using Bookify.Domain.UnitTests.Apartments;
using Bookify.Domain.UnitTests.Infrastructure;
using Bookify.Domain.UnitTests.Users;
using Bookify.Domain.Users;
using FluentAssertions;

namespace Bookify.Domain.UnitTests.Bookings;

public class BookingTests : BaseTest
{
    [Fact]
    public void Reserve_ShouldRaiseBookingReservedDomainEvent()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();

        // Act
        var booking = Booking.Reserve(apartment, user.Id, period, DateTime.UtcNow, pricingService);

        // Assert
        BookingReservedDomainEvent domainEvent = AssertDomainEventWasPublished<BookingReservedDomainEvent>(booking);

        domainEvent.BookingId.Should().Be(booking.Id);
    }

    [Fact]
    public void Confirm_ShouldRaiseBookingConfirmedDomainEvent()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");

        // Act
        Result result = booking.Confirm(utcNow);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ConfirmedOnUtc.Should().Be(utcNow);

        BookingConfirmedDomainEvent domainEvent = AssertDomainEventWasPublished<BookingConfirmedDomainEvent>(booking);

        domainEvent.BookingId.Should().Be(booking.Id);
    }

    [Fact]
    public void Reserve_ShouldSetApartmentLastBookedOnUtc()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        // Act
        Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);

        // Assert
        apartment.LastBookedOnUtc.Should().Be(utcNow);
    }

    [Fact]
    public void Cancel_ShouldReturnFailure_WhenStatusIsInvalid()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Reject(utcNow); // Rejected status is invalid for cancel

        // Act
        var result = booking.Cancel(utcNow, null, null, false);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotCancellable);
    }

    [Fact]
    public void Cancel_ShouldReturnFailure_WhenDateHasStarted()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(utcNow);

        DateTime cancellationDate = new(2025, 12, 2, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var result = booking.Cancel(cancellationDate, null, null, false);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.AlreadyStarted);
    }

    [Fact]
    public void Cancel_ShouldSucceed_WhenValid()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(utcNow);

        DateTime cancellationDate = new(2025, 11, 30, 12, 0, 0, DateTimeKind.Utc);
        var policy = CancellationPolicy.Create("Test Policy", 0.00m, 0.50m, 0.10m, 1.00m, 24, true, utcNow);
        var engine = new CancellationPolicyEngine();

        // Act
        var result = booking.Cancel(cancellationDate, policy, engine, false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.CancelledOnUtc.Should().Be(cancellationDate);

        BookingCancelledDomainEvent domainEvent = AssertDomainEventWasPublished<BookingCancelledDomainEvent>(booking);

        domainEvent.BookingId.Should().Be(booking.Id);
    }

    [Fact]
    public void Reject_ShouldReturnFailure_WhenStatusIsNotReserved()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(utcNow); // Change status to Confirmed

        // Act
        Result result = booking.Reject(utcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotReserved);
    }

    [Fact]
    public void Reject_ShouldSucceed_WhenStatusIsReserved()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");

        DateTime rejectionDate = new(2025, 11, 30, 12, 0, 0, DateTimeKind.Utc);

        // Act
        Result result = booking.Reject(rejectionDate);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Rejected);
        booking.RejectedOnUtc.Should().Be(rejectionDate);

        BookingRejectedDomainEvent domainEvent = AssertDomainEventWasPublished<BookingRejectedDomainEvent>(booking);

        domainEvent.BookingId.Should().Be(booking.Id);
    }

    [Fact]
    public void Complete_ShouldReturnFailure_WhenStatusIsNotInProgress()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);

        // Act
        Result result = booking.Complete(utcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotInProgress);
    }

    [Fact]
    public void Complete_ShouldSucceed_WhenStatusIsInProgress()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(utcNow);
        booking.CheckIn(new DateTime(2025, 12, 1, 12, 0, 0, DateTimeKind.Utc));

        DateTime completionDate = new(2025, 12, 16, 12, 0, 0, DateTimeKind.Utc);

        // Act
        Result result = booking.Complete(completionDate);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Completed);
        booking.CompletedOnUtc.Should().Be(completionDate);

        BookingCompletedDomainEvent domainEvent = AssertDomainEventWasPublished<BookingCompletedDomainEvent>(booking);

        domainEvent.BookingId.Should().Be(booking.Id);
    }

    [Fact]
    public void Cancel_ShouldSucceed_WhenStatusIsReserved()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");

        // Act
        var result = booking.Cancel(utcNow, null, null, false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.CancelledOnUtc.Should().Be(utcNow);

        BookingCancelledDomainEvent domainEvent = AssertDomainEventWasPublished<BookingCancelledDomainEvent>(booking);
        domainEvent.BookingId.Should().Be(booking.Id);
    }

    [Fact]
    public void Cancel_ShouldSucceed_WhenStatusIsPendingPayment()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);

        // Act
        var result = booking.Cancel(utcNow, null, null, false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.CancelledOnUtc.Should().Be(utcNow);
        booking.PaymentStatus.Should().Be(PaymentStatus.Unpaid);

        BookingCancelledDomainEvent domainEvent = AssertDomainEventWasPublished<BookingCancelledDomainEvent>(booking);
        domainEvent.BookingId.Should().Be(booking.Id);
        domainEvent.RefundAmount.Should().BeNull();
        domainEvent.CancelledByHost.Should().BeFalse();
    }

    [Fact]
    public void Cancel_ShouldFail_WhenStatusIsPendingPaymentAndCancelledByHost()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);

        // Act
        var result = booking.Cancel(utcNow, null, null, true);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotCancellable);
    }

    [Fact]
    public void Cancel_ShouldRaiseBookingCancelledDomainEvent_WithRefundDetails()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = new(2025, 11, 29, 12, 0, 0, DateTimeKind.Utc);

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.MarkAsPaid("intent-id", utcNow);

        var policy = CancellationPolicy.Create("Test Policy", 0.00m, 0.50m, 0.10m, 1.00m, 24, true, utcNow);
        var engine = new CancellationPolicyEngine();

        // Act
        var result = booking.Cancel(utcNow, policy, engine, true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.CancelledOnUtc.Should().Be(utcNow);

        BookingCancelledDomainEvent domainEvent = AssertDomainEventWasPublished<BookingCancelledDomainEvent>(booking);
        domainEvent.BookingId.Should().Be(booking.Id);
        domainEvent.RefundAmount.Should().Be(140.0m);
        domainEvent.CancelledByHost.Should().BeTrue();
        domainEvent.Currency.Should().Be("USD");
        domainEvent.HostPenaltyAmount.Should().Be(14.0m);
    }

    [Fact]
    public void CheckIn_ShouldReturnFailure_WhenStatusIsNotConfirmed()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);

        // Act
        Result result = booking.CheckIn(new DateTime(2025, 12, 1, 12, 0, 0, DateTimeKind.Utc));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotConfirmed);
    }

    [Fact]
    public void CheckIn_ShouldSucceed_WhenStatusIsConfirmed()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(utcNow);

        // Act
        DateTime checkInDate = new DateTime(2025, 12, 1, 12, 0, 0, DateTimeKind.Utc);
        Result result = booking.CheckIn(checkInDate);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.InProgress);
        booking.CheckedInOnUtc.Should().Be(checkInDate);

        BookingCheckedInDomainEvent domainEvent = AssertDomainEventWasPublished<BookingCheckedInDomainEvent>(booking);
        domainEvent.BookingId.Should().Be(booking.Id);
    }

    [Fact]
    public void MarkNoShow_ShouldReturnFailure_WhenStatusIsNotConfirmed()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 15), new DateOnly(2025, 12, 20));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);

        // Act
        Result result = booking.MarkNoShow(utcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotConfirmed);
    }

    [Fact]
    public void MarkNoShow_ShouldReturnFailure_WhenNowDateIsNotAfterStartDate()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 15), new DateOnly(2025, 12, 20));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(utcNow);

        // Act - marking no-show on the day before the start date (not after)
        DateTime checkDate = new(2025, 12, 14, 12, 0, 0, DateTimeKind.Utc);
        Result result = booking.MarkNoShow(checkDate);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.CannotMarkNoShow);
    }

    [Fact]
    public void MarkNoShow_ShouldSucceed_WhenStatusIsConfirmedAndNowDateIsAfterStartDate()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 15), new DateOnly(2025, 12, 20));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(utcNow);

        // Act - marking no-show on the day after the start date
        DateTime checkDate = new(2025, 12, 16, 12, 0, 0, DateTimeKind.Utc);
        Result result = booking.MarkNoShow(checkDate);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.NoShow);
        booking.NoShowAt.Should().Be(checkDate);

        BookingNoShowDomainEvent domainEvent = AssertDomainEventWasPublished<BookingNoShowDomainEvent>(booking);
        domainEvent.BookingId.Should().Be(booking.Id);
    }

    [Fact]
    public void Expire_ShouldReturnFailure_WhenStatusIsNotReservedOrPendingPayment()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(utcNow);

        // Act
        Result result = booking.Expire(utcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotExpirable);
    }

    [Fact]
    public void Expire_ShouldSucceed_WhenStatusIsReserved()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");

        // Act
        Result result = booking.Expire(utcNow);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Expired);
        booking.ExpiredOnUtc.Should().Be(utcNow);

        BookingExpiredDomainEvent domainEvent = AssertDomainEventWasPublished<BookingExpiredDomainEvent>(booking);
        domainEvent.BookingId.Should().Be(booking.Id);
    }

    [Fact]
    public void Expire_ShouldSucceed_WhenStatusIsPendingPayment()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);

        // Act
        Result result = booking.Expire(utcNow);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Expired);
        booking.ExpiredOnUtc.Should().Be(utcNow);

        BookingExpiredDomainEvent domainEvent = AssertDomainEventWasPublished<BookingExpiredDomainEvent>(booking);
        domainEvent.BookingId.Should().Be(booking.Id);
    }

    [Fact]
    public void AuthorizePayment_ShouldSucceed_WhenPendingPayment()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);

        // Act
        Result result = booking.AuthorizePayment("session_123", "intent_123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Reserved);
        booking.PaymentStatus.Should().Be(PaymentStatus.Authorized);

        BookingPaymentAuthorizedDomainEvent domainEvent = AssertDomainEventWasPublished<BookingPaymentAuthorizedDomainEvent>(booking);
        domainEvent.BookingId.Should().Be(booking.Id);
        domainEvent.StripeSessionId.Should().Be("session_123");
        domainEvent.StripePaymentIntentId.Should().Be("intent_123");
    }

    [Fact]
    public void AuthorizePayment_ShouldFail_WhenNotPendingPayment()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");

        // Act
        Result result = booking.AuthorizePayment("session_123", "intent_123");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotPendingPayment);
    }

    [Fact]
    public void MarkAsPaid_ShouldSucceed_WhenPendingPayment()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);

        // Act
        Result result = booking.MarkAsPaid("intent_123", utcNow);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.PaymentStatus.Should().Be(PaymentStatus.Paid);
        booking.ConfirmedOnUtc.Should().Be(utcNow);

        BookingPaymentCompletedDomainEvent domainEvent = AssertDomainEventWasPublished<BookingPaymentCompletedDomainEvent>(booking);
        domainEvent.BookingId.Should().Be(booking.Id);
        domainEvent.StripePaymentIntentId.Should().Be("intent_123");
    }

    [Fact]
    public void MarkAsPaid_ShouldFail_WhenNotPendingPayment()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");

        // Act
        Result result = booking.MarkAsPaid("intent_123", utcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotPendingPayment);
    }

    [Fact]
    public void InitiateRefund_ShouldSucceed_WhenCancelledAndPaid()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = new(2025, 11, 30, 12, 0, 0, DateTimeKind.Utc);

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.MarkAsPaid("intent_123", utcNow);
        var policy = CancellationPolicy.Create("Policy", 0m, 0.5m, 0.1m, 1m, 24, true, utcNow);
        var engine = new CancellationPolicyEngine();
        booking.Cancel(utcNow, policy, engine, false);

        // Act
        Result result = booking.InitiateRefund(100.0m, "USD", "Guest cancellation");

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.PaymentStatus.Should().Be(PaymentStatus.RefundProcessing);

        BookingRefundInitiatedDomainEvent domainEvent = AssertDomainEventWasPublished<BookingRefundInitiatedDomainEvent>(booking);
        domainEvent.BookingId.Should().Be(booking.Id);
        domainEvent.RefundAmount.Should().Be(100.0m);
        domainEvent.Currency.Should().Be("USD");
        domainEvent.Reason.Should().Be("Guest cancellation");
    }

    [Fact]
    public void Cancel_ShouldReleaseAuthorization_WhenReservedAndAuthorized()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = new(2025, 11, 30, 12, 0, 0, DateTimeKind.Utc);

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session_123", "intent_123");

        // Act
        var result = booking.Cancel(utcNow, null, null, false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.PaymentStatus.Should().Be(PaymentStatus.AuthorizationReleased);
    }

    [Fact]
    public void Reject_ShouldReleaseAuthorization_WhenReservedAndAuthorized()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = new(2025, 11, 30, 12, 0, 0, DateTimeKind.Utc);

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session_123", "intent_123");

        // Act
        Result result = booking.Reject(utcNow);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.PaymentStatus.Should().Be(PaymentStatus.AuthorizationReleased);
    }

    [Fact]
    public void Expire_ShouldReleaseAuthorization_WhenReservedAndAuthorized()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = new(2025, 11, 30, 12, 0, 0, DateTimeKind.Utc);

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session_123", "intent_123");

        // Act
        Result result = booking.Expire(utcNow);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.PaymentStatus.Should().Be(PaymentStatus.AuthorizationReleased);
    }

    [Fact]
    public void InitiateRefund_ShouldFail_WhenNotCancelled()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = new(2025, 11, 30, 12, 0, 0, DateTimeKind.Utc);

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.MarkAsPaid("intent_123", utcNow);

        // Act
        Result result = booking.InitiateRefund(100.0m, "USD", "Guest cancellation");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.RefundNotEligible);
    }

    [Fact]
    public void InitiateRefund_ShouldFail_WhenUnpaid()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = new(2025, 11, 30, 12, 0, 0, DateTimeKind.Utc);

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.Cancel(utcNow, null, null, false);

        // Act
        Result result = booking.InitiateRefund(100.0m, "USD", "Guest cancellation");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.RefundNotEligible);
    }

    [Fact]
    public void CheckIn_ShouldReturnFailure_WhenGuestCheckInDateIsAfterUtcNow()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = new(2025, 12, 5, 12, 0, 0, DateTimeKind.Utc);

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(utcNow);

        // Act - Guest check-in date is in the future (after today/utcNow)
        DateOnly invalidCheckInDate = new(2025, 12, 6);
        Result result = booking.CheckIn(utcNow, invalidCheckInDate);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.InvalidCheckInDate);
    }

    [Fact]
    public void CheckIn_ShouldReturnFailure_WhenGuestCheckInDateIsBeforeStartDate()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 2), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = new(2025, 12, 5, 12, 0, 0, DateTimeKind.Utc);

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(utcNow);

        // Act - Guest check-in date is before the booking duration start date
        DateOnly invalidCheckInDate = new(2025, 12, 1);
        Result result = booking.CheckIn(utcNow, invalidCheckInDate);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.InvalidCheckInDate);
    }

    [Fact]
    public void CheckOut_ShouldReturnFailure_WhenStatusIsNotInProgress()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = new(2025, 12, 5, 12, 0, 0, DateTimeKind.Utc);

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);

        // Act
        Result result = booking.CheckOut(utcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotInProgress);
    }

    [Fact]
    public void CheckOut_ShouldReturnFailure_WhenGuestCheckOutDateIsAfterUtcNow()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = new(2025, 12, 5, 12, 0, 0, DateTimeKind.Utc);

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(utcNow);
        booking.CheckIn(utcNow);

        // Act - Guest check-out date is in the future (after today/utcNow)
        DateOnly invalidCheckOutDate = new(2025, 12, 6);
        Result result = booking.CheckOut(utcNow, guestCheckOutDate: invalidCheckOutDate);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.InvalidCheckOutDate);
    }

    [Fact]
    public void CheckOut_ShouldReturnFailure_WhenGuestCheckOutDateIsBeforeStartDate()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 2), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = new(2025, 12, 5, 12, 0, 0, DateTimeKind.Utc);

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(utcNow);
        booking.CheckIn(utcNow);

        // Act - Guest check-out date is before the duration start date (or check-in start date)
        DateOnly invalidCheckOutDate = new(2025, 12, 1);
        Result result = booking.CheckOut(utcNow, guestCheckOutDate: invalidCheckOutDate);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.InvalidCheckOutDate);
    }

    [Fact]
    public void CheckOut_ShouldSucceed_WhenStatusIsInProgress()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = new(2025, 12, 5, 12, 0, 0, DateTimeKind.Utc);

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(utcNow);
        booking.CheckIn(utcNow);

        // Act
        Result result = booking.CheckOut(utcNow);

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Completed);
        booking.CompletedOnUtc.Should().Be(utcNow);

        BookingCheckedOutDomainEvent domainEvent = AssertDomainEventWasPublished<BookingCheckedOutDomainEvent>(booking);
        domainEvent.BookingId.Should().Be(booking.Id);
    }

    [Fact]
    public void CloseStay_ShouldReturnFailure_WhenStatusIsNotConfirmed()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = new(2025, 12, 20, 12, 0, 0, DateTimeKind.Utc);

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);

        // Act - booking is only Reserved, not Confirmed
        Result result = booking.CloseStay(utcNow, new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotConfirmed);
    }

    [Fact]
    public void CloseStay_ShouldReturnFailure_WhenStayNotYetEnded()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = new(2025, 12, 10, 12, 0, 0, DateTimeKind.Utc);

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(utcNow);

        // Act - today is 2025-12-10, booking ends on 2025-12-15 (stay has not yet ended)
        Result result = booking.CloseStay(utcNow, new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.StayNotYetEnded);
    }

    [Fact]
    public void CloseStay_ShouldReturnFailure_WhenCheckInDateIsInvalid()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 2), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = new(2025, 12, 20, 12, 0, 0, DateTimeKind.Utc);

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(utcNow);

        // Act - check-in date (2025-12-01) is before Duration.Start (2025-12-02)
        Result result = booking.CloseStay(utcNow, new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.InvalidCheckInDate);
    }

    [Fact]
    public void CloseStay_ShouldReturnFailure_WhenCheckOutDateIsInvalid()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = new(2025, 12, 20, 12, 0, 0, DateTimeKind.Utc);

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(utcNow);

        // Act - check-out date (2025-11-30) is before check-in date (2025-12-01)
        Result result = booking.CloseStay(utcNow, new DateOnly(2025, 12, 1), new DateOnly(2025, 11, 30));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.InvalidCheckOutDate);
    }

    [Fact]
    public void CloseStay_ShouldSucceed_WhenValid()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = new(2025, 12, 20, 12, 0, 0, DateTimeKind.Utc);

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(utcNow);

        // Act
        Result result = booking.CloseStay(utcNow, new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));

        // Assert
        result.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Completed);
        booking.CheckedInOnUtc.Should().Be(new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc));
        booking.CompletedOnUtc.Should().Be(new DateTime(2025, 12, 15, 0, 0, 0, DateTimeKind.Utc));

        BookingClosedStayDomainEvent domainEvent = AssertDomainEventWasPublished<BookingClosedStayDomainEvent>(booking);
        domainEvent.BookingId.Should().Be(booking.Id);
    }
}
