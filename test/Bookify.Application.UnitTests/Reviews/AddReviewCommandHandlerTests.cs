using System.Linq.Expressions;
using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Reviews.AddReview;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Reviews;
using Bookify.Domain.Shared;
using FluentAssertions;
using NSubstitute;

namespace Bookify.Application.UnitTests.Reviews;

public class AddReviewCommandHandlerTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;

    private readonly AddReviewCommandHandler _handler;

    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IReviewRepository _reviewRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IUserContext _userContextMock;

    public AddReviewCommandHandlerTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _reviewRepositoryMock = Substitute.For<IReviewRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _userContextMock = Substitute.For<IUserContext>();

        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _handler = new AddReviewCommandHandler(
            _bookingRepositoryMock,
            _reviewRepositoryMock,
            _unitOfWorkMock,
            _dateTimeProviderMock,
            _userContextMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingIsNotFound()
    {
        // Arrange
        var command = new AddReviewCommand(Guid.CreateVersion7(), 5, "Great");

        _bookingRepositoryMock
            .GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns((Domain.Bookings.Booking?)null);

        // Act
        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Error.Should().Be(BookingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserIsNotOwner()
    {
        // Arrange
        var command = new AddReviewCommand(Guid.CreateVersion7(), 5, "Great");

        var ownerId = Guid.CreateVersion7();
        var callerId = Guid.CreateVersion7(); // Different from owner

        var apartment = Apartment.Create(
            Guid.CreateVersion7(),
            new Name("Test Apartment"),
            new Description("Description"),
            new Address("Country", "State", "Zip", "City", "Street"),
            new Money(100, Currency.Usd),
            new Money(10, Currency.Usd),
            [],
            DateTime.UtcNow);

        var pricingService = new PricingService();
        var booking = Domain.Bookings.Booking.Reserve(apartment, ownerId, DateRange.Create(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10)), UtcNow, pricingService);

        _bookingRepositoryMock
            .GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _userContextMock.UserId.Returns(callerId);

        // Act
        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Error.Should().Be(ReviewErrors.Unauthorized);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenAlreadyReviewed()
    {
        // Arrange
        var command = new AddReviewCommand(Guid.CreateVersion7(), 5, "Great");
        var userId = Guid.CreateVersion7();

        var apartment = Apartment.Create(Guid.CreateVersion7(),
            new Name("Test"),
            new Description("-"),
            new Address("a",
                "b",
                "c",
                "d",
                "e"),
            new Money(1,
                Currency.Usd),
            new Money(1,
                Currency.Usd),
            [],
            DateTime.UtcNow);
        var booking = Domain.Bookings.Booking.Reserve(apartment, userId, DateRange.Create(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10)), UtcNow, new PricingService());

        _bookingRepositoryMock
            .GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _userContextMock.UserId.Returns(userId);

        _reviewRepositoryMock
            .ExistsAsync(Arg.Any<Expression<Func<Review, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(true); // Already reviewed

        // Act
        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Error.Should().Be(ReviewErrors.AlreadyReviewed);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenRatingIsInvalid()
    {
        // Arrange
        var command = new AddReviewCommand(Guid.CreateVersion7(), 99, "Great"); // Invalid rating
        var userId = Guid.CreateVersion7();

        var apartment = Apartment.Create(Guid.CreateVersion7(),
            new Name("Test"),
            new Description("-"),
            new Address("a",
                "b",
                "c",
                "d",
                "e"),
            new Money(1,
                Currency.Usd),
            new Money(1,
                Currency.Usd),
            [],
            DateTime.UtcNow);
        var booking = Domain.Bookings.Booking.Reserve(apartment, userId, DateRange.Create(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10)), UtcNow, new PricingService());
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(UtcNow);
        booking.CheckIn(UtcNow);
        booking.Complete(UtcNow); // Required to pass the state validation if rating had been valid

        _bookingRepositoryMock
            .GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _userContextMock.UserId.Returns(userId);

        _reviewRepositoryMock
            .ExistsAsync(Arg.Any<Expression<Func<Review, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Rating.Invalid");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenBookingIsNotCompleted()
    {
        // Arrange
        var command = new AddReviewCommand(Guid.CreateVersion7(), 5, "Great");
        var userId = Guid.CreateVersion7();

        var apartment = Apartment.Create(
            Guid.CreateVersion7(),
            new Name("Test"),
            new Description("-"),
            new Address("a", "b", "c", "d", "e"),
            new Money(1, Currency.Usd),
            new Money(1, Currency.Usd),
            [],
            DateTime.UtcNow);

        var booking = Domain.Bookings.Booking.Reserve(apartment, userId, DateRange.Create(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10)), UtcNow, new PricingService());
        // Booking is Reserved, not Completed

        _bookingRepositoryMock
            .GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _userContextMock.UserId.Returns(userId);

        _reviewRepositoryMock
            .ExistsAsync(Arg.Any<Expression<Func<Review, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Error.Should().Be(ReviewErrors.NotEligible);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_AndCallRepositoryAndUnitOfWork()
    {
        // Arrange
        var command = new AddReviewCommand(Guid.CreateVersion7(), 5, "Great");
        var userId = Guid.CreateVersion7();

        var apartment = Apartment.Create(
            Guid.CreateVersion7(),
            new Name("Test"),
            new Description("-"),
            new Address("a", "b", "c", "d", "e"),
            new Money(1, Currency.Usd),
            new Money(1, Currency.Usd),
            [],
            DateTime.UtcNow);

        var booking = Domain.Bookings.Booking.Reserve(apartment, userId, DateRange.Create(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10)), UtcNow, new PricingService());
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(UtcNow);
        booking.CheckIn(UtcNow);
        booking.Complete(UtcNow); // Valid state

        _bookingRepositoryMock
            .GetByIdAsync(command.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _userContextMock.UserId.Returns(userId);

        _reviewRepositoryMock
            .ExistsAsync(Arg.Any<Expression<Func<Review, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result<Guid> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        _reviewRepositoryMock.Received(1).Add(Arg.Is<Review>(r => r.Id == result.Value));
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
