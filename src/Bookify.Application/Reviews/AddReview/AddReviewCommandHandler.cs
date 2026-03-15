using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Bookify.Domain.Reviews;

namespace Bookify.Application.Reviews.AddReview;

internal sealed class AddReviewCommandHandler : ICommandHandler<AddReviewCommand, Guid>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IReviewRepository _reviewRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUserContext _userContext;

    public AddReviewCommandHandler(
        IBookingRepository bookingRepository,
        IReviewRepository reviewRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IUserContext userContext)
    {
        _bookingRepository = bookingRepository;
        _reviewRepository = reviewRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _userContext = userContext;
    }

    public async Task<Result<Guid>> Handle(AddReviewCommand request, CancellationToken cancellationToken)
    {
        Booking? booking = await _bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure<Guid>(BookingErrors.NotFound);
        }

        if (booking.UserId != _userContext.UserId)
        {
            return Result.Failure<Guid>(ReviewErrors.Unauthorized);
        }

        bool alreadyReviewed = await _reviewRepository.ExistsAsync(r => r.BookingId == request.BookingId, cancellationToken);

        if (alreadyReviewed)
        {
            return Result.Failure<Guid>(ReviewErrors.AlreadyReviewed);
        }

        Result<Rating> ratingResult = Rating.Create(request.Rating);

        if (ratingResult.IsFailure)
        {
            return Result.Failure<Guid>(ratingResult.Error);
        }

        Result<Review> reviewResult = Review.Create(
            booking,
            ratingResult.Value,
            new Comment(request.Comment),
            _dateTimeProvider.UtcNow);

        if (reviewResult.IsFailure)
        {
            return Result.Failure<Guid>(reviewResult.Error);
        }

        _reviewRepository.Add(reviewResult.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(reviewResult.Value.Id);
    }
}
