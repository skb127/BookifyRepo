using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
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
    public void Reserve_WithInstantBooking_ShouldSetStatusConfirmed()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();

        // Act
        var booking = Booking.Reserve(apartment, user.Id, period, DateTime.UtcNow, pricingService, instantBooking: true);

        // Assert
        booking.Status.Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public void Reserve_WithInstantBooking_ShouldSetConfirmedOnUtc()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        var utcNow = DateTime.UtcNow;

        // Act
        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService, instantBooking: true);

        // Assert
        booking.ConfirmedOnUtc.Should().Be(utcNow);
    }

    [Fact]
    public void Reserve_WithInstantBooking_ShouldRaiseBookingConfirmedDomainEvent()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email,
            DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();

        // Act
        var booking = Booking.Reserve(apartment, user.Id, period, DateTime.UtcNow, pricingService, instantBooking: true);

        // Assert
        BookingConfirmedDomainEvent domainEvent = AssertDomainEventWasPublished<BookingConfirmedDomainEvent>(booking);

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
    public void Cancel_ShouldReturnFailure_WhenStatusIsNotConfirmed()
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
        Result result = booking.Cancel(utcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BookingErrors.NotConfirmed);
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
        booking.Confirm(utcNow);

        DateTime cancellationDate = new(2025, 12, 2, 12, 0, 0, DateTimeKind.Utc);

        // Act
        Result result = booking.Cancel(cancellationDate);

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
        booking.Confirm(utcNow);

        DateTime cancellationDate = new(2025, 11, 30, 12, 0, 0, DateTimeKind.Utc);

        // Act
        Result result = booking.Cancel(cancellationDate);

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
    public void Complete_ShouldReturnFailure_WhenStatusIsNotConfirmed()
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
        result.Error.Should().Be(BookingErrors.NotConfirmed);
    }

    [Fact]
    public void Complete_ShouldSucceed_WhenStatusIsConfirmed()
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
        booking.Confirm(utcNow);

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
}
