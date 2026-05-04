using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Options;
using Bookify.Application.Reviews.UpdateReview;
using Bookify.Application.UnitTests.Apartments;
using Bookify.Domain.Apartments;
using Bookify.Domain.Reviews;
using Bookify.Domain.Reviews.Events;
using Bookify.Domain.Users;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Bookify.Application.UnitTests.Reviews.Events;

public class ReviewUpdatedDomainEventHandlerTests
{
    private readonly IReviewRepository _reviewRepositoryMock;
    private readonly IUserRepository _userRepositoryMock;
    private readonly IApartmentRepository _apartmentRepositoryMock;
    private readonly IEmailService _emailServiceMock;
    private readonly IEmailTemplateService _emailTemplateServiceMock;
    private readonly ReviewUpdatedDomainEventHandler _handler;

    public ReviewUpdatedDomainEventHandlerTests()
    {
        _reviewRepositoryMock = Substitute.For<IReviewRepository>();
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _apartmentRepositoryMock = Substitute.For<IApartmentRepository>();
        _emailServiceMock = Substitute.For<IEmailService>();
        _emailTemplateServiceMock = Substitute.For<IEmailTemplateService>();

        var options = Microsoft.Extensions.Options.Options.Create(new BookifyAppOptions
        {
            FrontendUrl = new Uri("https://test.bookify.com")
        });

        _handler = new ReviewUpdatedDomainEventHandler(
            _reviewRepositoryMock,
            _userRepositoryMock,
            _apartmentRepositoryMock,
            _emailServiceMock,
            _emailTemplateServiceMock,
            options);
    }

    private static User CreateUser()
    {
        var firstName = new FirstName("Test");
        var lastName = new LastName("User");
        var email = new Email("test@test.com");
        var dateOfBirth = DateOfBirth.Create(new DateOnly(2000, 1, 1));

        return User.Create(firstName, lastName, email, dateOfBirth);
    }

    [Fact]
    public async Task Handle_ShouldNotSendEmail_WhenReviewNotFound()
    {
        // Arrange
        var domainEvent = new ReviewUpdatedDomainEvent(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        _reviewRepositoryMock.GetByIdAsync(domainEvent.ReviewId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        await _handler.Handle(domainEvent, default);

        // Assert
        await _emailTemplateServiceMock.DidNotReceiveWithAnyArgs().GenerateEmailBodyAsync(default!, default!);
        await _emailServiceMock.DidNotReceiveWithAnyArgs().SendAsync(default!);
    }

    [Fact]
    public async Task Handle_ShouldNotSendEmail_WhenUserNotFound()
    {
        // Arrange
        var domainEvent = new ReviewUpdatedDomainEvent(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        // Create an empty Review using reflection 
        var review = (Review)Activator.CreateInstance(typeof(Review), true)!;
        typeof(Review).GetProperty("UserId")!.SetValue(review, Guid.CreateVersion7());
        typeof(Review).GetProperty("Rating")!.SetValue(review, Rating.Create(5).Value);

        _reviewRepositoryMock.GetByIdAsync(domainEvent.ReviewId, Arg.Any<CancellationToken>())
            .Returns(review);

        _userRepositoryMock.GetByIdAsync(review.UserId, Arg.Any<CancellationToken>())
            .ReturnsNull(); // User not found

        // Act
        await _handler.Handle(domainEvent, default);

        // Assert
        // We now expect execution to halt before sending any emails
        await _emailTemplateServiceMock.DidNotReceiveWithAnyArgs().GenerateEmailBodyAsync(default!, default!);
        await _emailServiceMock.DidNotReceiveWithAnyArgs().SendAsync(default!);
    }

    [Fact]
    public async Task Handle_ShouldSendEmail_WhenValid()
    {
        // Arrange
        var domainEvent = new ReviewUpdatedDomainEvent(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        var user = CreateUser();
        var apartment = ApartmentData.Create();

        var review = (Review)Activator.CreateInstance(typeof(Review), true)!;
        typeof(Review).GetProperty("UserId")!.SetValue(review, user.Id);
        typeof(Review).GetProperty("ApartmentId")!.SetValue(review, apartment.Id);
        typeof(Review).GetProperty("Rating")!.SetValue(review, Rating.Create(5).Value);

        _reviewRepositoryMock.GetByIdAsync(domainEvent.ReviewId, Arg.Any<CancellationToken>())
            .Returns(review);

        _userRepositoryMock.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        
        _apartmentRepositoryMock.GetByIdAsync(apartment.Id, Arg.Any<CancellationToken>())
            .Returns(apartment);

        string expectedEmailBody = "<html>Email Content</html>";

        _emailTemplateServiceMock.GenerateEmailBodyAsync(
            "ReviewUpdated.html",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedEmailBody);

        // Act
        await _handler.Handle(domainEvent, default);

        // Assert
        await _emailTemplateServiceMock.Received(1).GenerateEmailBodyAsync(
            "ReviewUpdated.html",
            Arg.Is<object>(m =>
                m.GetType().GetProperty("FirstName")!.GetValue(m)!.ToString() == "Host" &&
                m.GetType().GetProperty("ReviewerName")!.GetValue(m)!.ToString() == user.FirstName.Value &&
                m.GetType().GetProperty("ApartmentName")!.GetValue(m)!.ToString() == apartment.Name.Value &&
                m.GetType().GetProperty("ApartmentId")!.GetValue(m)!.ToString() == apartment.Id.ToString() &&
                m.GetType().GetProperty("Rating")!.GetValue(m)!.ToString() == "5" &&
                m.GetType().GetProperty("HomeUrl")!.GetValue(m)!.ToString() == "https://test.bookify.com/"),
            Arg.Any<CancellationToken>());

        await _emailServiceMock.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m =>
                m.To == "host@bookify.com" &&
                m.Subject == $"A review for your apartment {apartment.Name.Value} has been updated" &&
                m.Body == expectedEmailBody),
            Arg.Any<CancellationToken>());
    }
}
