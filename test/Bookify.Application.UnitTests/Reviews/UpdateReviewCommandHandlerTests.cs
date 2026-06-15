using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Reviews.UpdateReview;
using Bookify.Application.UnitTests.Apartments;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Reviews;
using FluentAssertions;
using NSubstitute;

namespace Bookify.Application.UnitTests.Reviews;

public class UpdateReviewCommandHandlerTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;

    private readonly UpdateReviewCommandHandler _handler;

    private readonly IReviewRepository _reviewRepositoryMock;
    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IUserContext _userContextMock;

    public UpdateReviewCommandHandlerTests()
    {
        _reviewRepositoryMock = Substitute.For<IReviewRepository>();
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _userContextMock = Substitute.For<IUserContext>();

        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _handler = new UpdateReviewCommandHandler(
            _reviewRepositoryMock,
            _bookingRepositoryMock,
            _unitOfWorkMock,
            _userContextMock,
            _dateTimeProviderMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenReviewIsNotFound()
    {
        // Arrange
        var command = new UpdateReviewCommand(Guid.CreateVersion7(), 5, "Updated");

        _reviewRepositoryMock
            .GetByIdAsync(command.ReviewId, Arg.Any<CancellationToken>())
            .Returns((Review?)null);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Error.Should().Be(ReviewErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserIsNotAuthor()
    {
        // Arrange
        var command = new UpdateReviewCommand(Guid.CreateVersion7(), 5, "Updated");

        var reviewAuthorId = Guid.CreateVersion7();
        var callerId = Guid.CreateVersion7(); // Different user

        Apartment apartment = ApartmentData.Create();
        var booking = Domain.Bookings.Booking.Reserve(apartment, callerId /* Different ID */, DateRange.Create(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10)), UtcNow, new PricingService());
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(UtcNow);
        booking.CheckIn(UtcNow);
        booking.Complete(UtcNow);

        var review = Review.Create(booking, Rating.Create(5).Value, new Comment("Great"), UtcNow).Value;

        // Change Review UserId via reflection to mimic the mismatch
        typeof(Review).GetProperty("UserId")!.SetValue(review, reviewAuthorId);

        _reviewRepositoryMock
            .GetByIdAsync(command.ReviewId, Arg.Any<CancellationToken>())
            .Returns(review);

        _userContextMock.UserId.Returns(callerId);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Error.Should().Be(ReviewErrors.NotAuthor);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUpdateValidationFails_EditTimeExpired()
    {
        // Arrange
        var command = new UpdateReviewCommand(Guid.CreateVersion7(), 5, "Updated");
        var userId = Guid.CreateVersion7();

        Apartment apartment = ApartmentData.Create();
        var booking = Domain.Bookings.Booking.Reserve(apartment, userId, DateRange.Create(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10)), UtcNow, new PricingService());
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(UtcNow);
        booking.CheckIn(UtcNow);
        booking.Complete(UtcNow);

        var review = Review.Create(booking, Rating.Create(5).Value, new Comment("Great"), UtcNow).Value;

        _reviewRepositoryMock
            .GetByIdAsync(command.ReviewId, Arg.Any<CancellationToken>())
            .Returns(review);

        _bookingRepositoryMock
            .GetByIdAsync(review.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _userContextMock.UserId.Returns(userId);

        // Advance mock time past the 7 days limit to explicitly trigger EditTimeExpired
        _dateTimeProviderMock.UtcNow.Returns(booking.CompletedOnUtc!.Value.AddDays(8));

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReviewErrors.EditTimeExpired);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_AndCallUnitOfWork()
    {
        // Arrange
        var command = new UpdateReviewCommand(Guid.CreateVersion7(), 4, "It was actually just OK");
        var userId = Guid.CreateVersion7();

        Apartment apartment = ApartmentData.Create();
        var booking = Domain.Bookings.Booking.Reserve(apartment, userId, DateRange.Create(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10)), UtcNow, new PricingService());
        booking.AuthorizePayment("session-id", "intent-id");
        booking.Confirm(UtcNow);
        booking.CheckIn(UtcNow);
        booking.Complete(UtcNow);

        var review = Review.Create(booking, Rating.Create(5).Value, new Comment("Great"), UtcNow).Value;

        _reviewRepositoryMock
            .GetByIdAsync(command.ReviewId, Arg.Any<CancellationToken>())
            .Returns(review);

        _userContextMock.UserId.Returns(userId);

        _bookingRepositoryMock
            .GetByIdAsync(review.BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        review.Rating.Value.Should().Be(command.Rating);
        review.Comment.Value.Should().Be(command.Comment);

        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
