using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Options;
using Bookify.Domain.Reviews;
using Bookify.Domain.Reviews.Events;
using Bookify.Domain.Users;
using MediatR;
using Microsoft.Extensions.Options;

namespace Bookify.Application.Reviews.UpdateReview;

internal sealed class ReviewUpdatedDomainEventHandler : INotificationHandler<ReviewUpdatedDomainEvent>
{
    private readonly IReviewRepository _reviewRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly BookifyAppOptions _appOptions;

    public ReviewUpdatedDomainEventHandler(
        IReviewRepository reviewRepository,
        IUserRepository userRepository,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        IOptions<BookifyAppOptions> appOptions)
    {
        _reviewRepository = reviewRepository;
        _userRepository = userRepository;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _appOptions = appOptions.Value;
    }

    public async Task Handle(ReviewUpdatedDomainEvent notification, CancellationToken cancellationToken)
    {
        Review? review = await _reviewRepository.GetByIdAsync(notification.ReviewId, cancellationToken);
        if (review is null)
        {
            return;
        }

        // Get the user who left the original review
        User? reviewer = await _userRepository.GetByIdAsync(review.UserId, cancellationToken);
        if (reviewer is null)
        {
            return;
        }

#pragma warning disable S1135 // Complete the task associated to this 'TODO' comment.
        // TODO: Get the apartment owner
        // Currently, Bookify does not have an explicit Apartment -> Owner relationship in the domain
        // but we can send a generic email to the admin or a dummy owner for now.
#pragma warning restore S1135 // Complete the task associated to this 'TODO' comment.
        // Since we don't have 'OwnerId' in Apartment, we will send an email to a dummy email (for testing purposes) 
        // ideally, it should go to the apartment owner.

        // In the future, change to apartment.Owner.Email
        const string hostEmail = "host@bookify.com";

        string reviewerName = reviewer.FirstName.Value;
        Uri homeUri = _appOptions.FrontendUrl;

        var model = new
        {
            FirstName = "Host", // Placeholder for the host name
            ReviewerName = reviewerName,
            Rating = review.Rating.Value,
            HomeUrl = homeUri.AbsoluteUri
        };

        string emailBody = await _emailTemplateService.GenerateEmailBodyAsync(
            "ReviewUpdated.html",
            model, cancellationToken);

        var emailMessage = new EmailMessage(
            hostEmail,
            "A review for your apartment has been updated",
            emailBody);

        await _emailService.SendAsync(emailMessage, cancellationToken);
    }
}
