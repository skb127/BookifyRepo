using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Authorization;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Users;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Reviews;
using Bookify.Domain.Users;

namespace Bookify.Application.Reviews.DeleteReview;

internal sealed class DeleteReviewCommandHandler : ICommandHandler<DeleteReviewCommand>
{
    private readonly IReviewRepository _reviewRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserContext _userContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DeleteReviewCommandHandler(
        IReviewRepository reviewRepository,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        IAuthorizationService authorizationService,
        IDateTimeProvider dateTimeProvider)
    {
        _reviewRepository = reviewRepository;
        _unitOfWork = unitOfWork;
        _userContext = userContext;
        _authorizationService = authorizationService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> Handle(DeleteReviewCommand request, CancellationToken cancellationToken)
    {
        Review? review = await _reviewRepository.GetByIdAsync(request.ReviewId, cancellationToken);

        if (review is null)
        {
            return Result.Failure(ReviewErrors.NotFound);
        }

        if (review.UserId != _userContext.UserId)
        {
            UserRolesResponse rolesResponse = await _authorizationService.GetRolesForUserAsync(_userContext.IdentityId);

            if (rolesResponse.Roles.All(r => r.Id != Role.Admin.Id))
            {
                return Result.Failure(ReviewErrors.NotAuthor);
            }
        }

        review.Delete(_dateTimeProvider.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
