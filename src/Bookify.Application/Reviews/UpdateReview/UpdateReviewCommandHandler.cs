using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Bookify.Domain.Reviews;

namespace Bookify.Application.Reviews.UpdateReview;

internal sealed class UpdateReviewCommandHandler :
    ICommandHandler<UpdateReviewCommand>
{
    private readonly IReviewRepository _reviewRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserContext _userContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateReviewCommandHandler(
        IReviewRepository reviewRepository,
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        IDateTimeProvider dateTimeProvider)
    {
        _reviewRepository = reviewRepository;
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
        _userContext = userContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> Handle(UpdateReviewCommand request, CancellationToken cancellationToken)
    {
        Review? review = await _reviewRepository.GetByIdAsync(request.ReviewId, cancellationToken);

        if (review is null)
        {
            return Result.Failure(ReviewErrors.NotFound);
        }

        if (review.UserId != _userContext.UserId)
        {
            return Result.Failure(ReviewErrors.NotAuthor);
        }

        Result<Rating> ratingResult = Rating.Create(request.Rating);
        if (ratingResult.IsFailure)
        {
            return Result.Failure(ratingResult.Error);
        }

        var comment = new Comment(request.Comment);

        Booking? booking = await _bookingRepository.GetByIdAsync(review.BookingId, cancellationToken);
        if (booking is null)
        {
            return Result.Failure(BookingErrors.NotFound);
        }

        Result updateResult = review.Update(
            ratingResult.Value,
            comment,
            _dateTimeProvider.UtcNow,
            booking);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
