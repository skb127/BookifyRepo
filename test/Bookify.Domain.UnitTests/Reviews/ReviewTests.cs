using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Reviews;
using Bookify.Domain.Reviews.Events;
using Bookify.Domain.Shared;
using Bookify.Domain.UnitTests.Apartments;
using Bookify.Domain.UnitTests.Infrastructure;
using Bookify.Domain.UnitTests.Users;
using Bookify.Domain.Users;
using FluentAssertions;

namespace Bookify.Domain.UnitTests.Reviews;

public class ReviewTests : BaseTest
{
    [Fact]
    public void Create_ShouldReturnFailure_WhenBookingIsNotCompleted()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email, DateOfBirth.Create(new DateOnly(2000, 1, 1))!);
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        // The booking is just Reserved, not Completed
        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);

        var rating = Rating.Create(5).Value;
        var comment = new Comment("Great place");

        // Act
        var result = Review.Create(booking, rating, comment, utcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReviewErrors.NotEligible);
    }

    [Fact]
    public void Create_ShouldReturnSuccess_WhenBookingIsCompleted()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email, DateOfBirth.Create(new DateOnly(2000, 1, 1))!);
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.Confirm(utcNow);
        booking.Complete(utcNow); // Status is now Completed

        var rating = Rating.Create(5).Value;
        var comment = new Comment("Great place");

        // Act
        var result = Review.Create(booking, rating, comment, utcNow);

        // Assert
        result.IsSuccess.Should().BeTrue();

        ReviewCreatedDomainEvent domainEvent = AssertDomainEventWasPublished<ReviewCreatedDomainEvent>(result.Value);
        domainEvent.ReviewId.Should().Be(result.Value.Id);
    }

    [Fact]
    public void Update_ShouldReturnFailure_WhenBookingDoesNotMatch()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email, DateOfBirth.Create(new DateOnly(2000, 1, 1))!);
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var bookingOriginal = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        bookingOriginal.Confirm(utcNow);
        bookingOriginal.Complete(utcNow);

        var bookingOther = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);

        var rating = Rating.Create(5).Value;
        var comment = new Comment("Great place");
        var review = Review.Create(bookingOriginal, rating, comment, utcNow).Value;

        var newRating = Rating.Create(4).Value;
        var newComment = new Comment("Decent place");

        // Act
        var result = review.Update(newRating, newComment, utcNow, bookingOther);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReviewErrors.NotEligible);
    }

    [Fact]
    public void Update_ShouldReturnFailure_WhenBookingIsNotCompleted()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email, DateOfBirth.Create(new DateOnly(2000, 1, 1))!);
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        // Force a review to exist
        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.Confirm(utcNow);
        booking.Complete(utcNow);
        var review = Review.Create(booking, Rating.Create(5).Value, new Comment("Great"), utcNow).Value;

        // Create a new booking that is NOT completed
        var uncompletedBooking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        // We override its ID to bypass the Identity check
        typeof(Entity).GetProperty("Id")!.SetValue(uncompletedBooking, booking.Id);

        // Act
        var result = review.Update(Rating.Create(4).Value, new Comment("Ok"), utcNow, uncompletedBooking);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReviewErrors.NotEligible);
    }

    [Fact]
    public void Update_ShouldReturnFailure_WhenEditTimeIsExpired()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email, DateOfBirth.Create(new DateOnly(2000, 1, 1))!);
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.Confirm(utcNow);
        booking.Complete(utcNow);
        var review = Review.Create(booking, Rating.Create(5).Value, new Comment("Great"), utcNow).Value;

        DateTime expiredDate = booking.CompletedOnUtc!.Value.AddDays(7).AddSeconds(1);

        // Act
        var result = review.Update(Rating.Create(4).Value, new Comment("Ok"), expiredDate, booking);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReviewErrors.EditTimeExpired);
    }

    [Fact]
    public void Update_ShouldReturnSuccess_WhenValid()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email, DateOfBirth.Create(new DateOnly(2000, 1, 1))!);
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.Confirm(utcNow);
        booking.Complete(utcNow);
        var review = Review.Create(booking, Rating.Create(5).Value, new Comment("Great"), utcNow).Value;

        var newRating = Rating.Create(4).Value;
        var newComment = new Comment("Decent place");
        DateTime editDate = utcNow.AddDays(2); // Valid (within 7 days)

        // Act
        var result = review.Update(newRating, newComment, editDate, booking);

        // Assert
        result.IsSuccess.Should().BeTrue();
        review.Rating.Should().Be(newRating);
        review.Comment.Should().Be(newComment);
        review.EditedOnUtc.Should().Be(editDate);

        ReviewUpdatedDomainEvent domainEvent = AssertDomainEventWasPublished<ReviewUpdatedDomainEvent>(review);
        domainEvent.ReviewId.Should().Be(review.Id);
        domainEvent.ApartmentId.Should().Be(apartment.Id);
        domainEvent.BookingId.Should().Be(booking.Id);
    }

    [Fact]
    public void Delete_ShouldSetDeletedOnUtc_WhenCalled()
    {
        // Arrange
        var user = User.Create(UserData.FirstName, UserData.LastName, UserData.Email, DateOfBirth.Create(new DateOnly(2000, 1, 1))!);
        var price = new Money(10.0m, Currency.Usd);
        var period = DateRange.Create(new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15));
        Apartment apartment = ApartmentData.Create(price);
        var pricingService = new PricingService();
        DateTime utcNow = DateTime.UtcNow;

        var booking = Booking.Reserve(apartment, user.Id, period, utcNow, pricingService);
        booking.Confirm(utcNow);
        booking.Complete(utcNow);
        var review = Review.Create(booking, Rating.Create(5).Value, new Comment("Great"), utcNow).Value;

        DateTime deleteDate = utcNow.AddDays(1);

        // Act
        review.Delete(deleteDate);

        // Assert
        review.DeletedOnUtc.Should().Be(deleteDate);
    }
}
