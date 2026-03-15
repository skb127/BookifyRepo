using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Authorization;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Reviews.DeleteReview;
using Bookify.Application.UnitTests.Apartments;
using Bookify.Application.Users;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Bookify.Domain.Reviews;
using Bookify.Domain.Shared;
using Bookify.Domain.Users;
using FluentAssertions;
using NSubstitute;

namespace Bookify.Application.UnitTests.Reviews;

public class DeleteReviewCommandHandlerTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;

    private readonly DeleteReviewCommandHandler _handler;

    private readonly IReviewRepository _reviewRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IUserContext _userContextMock;
    private readonly IAuthorizationService _authorizationServiceMock;
    private readonly IDateTimeProvider _dateTimeProviderMock;

    public DeleteReviewCommandHandlerTests()
    {
        _reviewRepositoryMock = Substitute.For<IReviewRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _userContextMock = Substitute.For<IUserContext>();
        _authorizationServiceMock = Substitute.For<IAuthorizationService>();

        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _handler = new DeleteReviewCommandHandler(
            _reviewRepositoryMock,
            _unitOfWorkMock,
            _userContextMock,
            _authorizationServiceMock,
            _dateTimeProviderMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenReviewIsNotFound()
    {
        // Arrange
        var command = new DeleteReviewCommand(Guid.CreateVersion7());

        _reviewRepositoryMock
            .GetByIdAsync(command.ReviewId, Arg.Any<CancellationToken>())
            .Returns((Review?)null);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Error.Should().Be(ReviewErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserIsNotAuthorAndNotAdmin()
    {
        // Arrange
        var command = new DeleteReviewCommand(Guid.CreateVersion7());

        var reviewAuthorId = Guid.CreateVersion7();
        var callerId = Guid.CreateVersion7();
        string callerIdentityId = "auth0|caller123";

        Domain.Apartments.Apartment apartment = ApartmentData.Create();
        var booking = Domain.Bookings.Booking.Reserve(apartment, callerId, DateRange.Create(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10)), UtcNow, new PricingService());
        booking.Confirm(UtcNow);
        booking.Complete(UtcNow);

        var review = Review.Create(booking, Rating.Create(5).Value, new Comment("Great"), UtcNow).Value;

        // Change Review UserId via reflection to mimic the mismatch
        typeof(Review).GetProperty("UserId")!.SetValue(review, reviewAuthorId);

        _reviewRepositoryMock
            .GetByIdAsync(command.ReviewId, Arg.Any<CancellationToken>())
            .Returns(review);

        _userContextMock.UserId.Returns(callerId);
        _userContextMock.IdentityId.Returns(callerIdentityId);

        // Mock authorization service to return only 'Registered' role
        var rolesResponse = new UserRolesResponse
        {
            Id = callerId,
            Roles = [Role.Registered]
        };
        
        _authorizationServiceMock
            .GetRolesForUserAsync(callerIdentityId)
            .Returns(rolesResponse);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReviewErrors.NotAuthor);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_AndCallUnitOfWork_WhenUserIsAuthor()
    {
        // Arrange
        var command = new DeleteReviewCommand(Guid.CreateVersion7());
        var userId = Guid.CreateVersion7();

        Domain.Apartments.Apartment apartment = ApartmentData.Create();
        var booking = Domain.Bookings.Booking.Reserve(apartment, userId, DateRange.Create(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10)), UtcNow, new PricingService());
        booking.Confirm(UtcNow);
        booking.Complete(UtcNow);

        var review = Review.Create(booking, Rating.Create(5).Value, new Comment("Great"), UtcNow).Value;

        _reviewRepositoryMock
            .GetByIdAsync(command.ReviewId, Arg.Any<CancellationToken>())
            .Returns(review);

        _userContextMock.UserId.Returns(userId);

        DateTime deleteDate = UtcNow.AddDays(1);
        _dateTimeProviderMock.UtcNow.Returns(deleteDate);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        review.DeletedOnUtc.Should().Be(deleteDate);

        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        // Ensure Authorization checks were NOT called since the user is the author
        await _authorizationServiceMock.DidNotReceive().GetRolesForUserAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_AndCallUnitOfWork_WhenUserIsAdmin()
    {
        // Arrange
        var command = new DeleteReviewCommand(Guid.CreateVersion7());

        var reviewAuthorId = Guid.CreateVersion7();
        var callerId = Guid.CreateVersion7();
        string callerIdentityId = "auth0|admin123";

        Domain.Apartments.Apartment apartment = ApartmentData.Create();
        var booking = Domain.Bookings.Booking.Reserve(apartment, reviewAuthorId, DateRange.Create(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 10)), UtcNow, new PricingService());
        booking.Confirm(UtcNow);
        booking.Complete(UtcNow);

        var review = Review.Create(booking, Rating.Create(5).Value, new Comment("Great"), UtcNow).Value;

        typeof(Review).GetProperty("UserId")!.SetValue(review, reviewAuthorId);

        _reviewRepositoryMock
            .GetByIdAsync(command.ReviewId, Arg.Any<CancellationToken>())
            .Returns(review);

        _userContextMock.UserId.Returns(callerId);
        _userContextMock.IdentityId.Returns(callerIdentityId);

        // Mock authorization service to return 'Admin' role
        var rolesResponse = new UserRolesResponse
        {
            Id = callerId,
            Roles = [Role.Registered, Role.Admin]
        };
        
        _authorizationServiceMock
            .GetRolesForUserAsync(callerIdentityId)
            .Returns(rolesResponse);

        DateTime deleteDate = UtcNow.AddDays(1);
        _dateTimeProviderMock.UtcNow.Returns(deleteDate);

        // Act
        Result result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        review.DeletedOnUtc.Should().Be(deleteDate);

        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
