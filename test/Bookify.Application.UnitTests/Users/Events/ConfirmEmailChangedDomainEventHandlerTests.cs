using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Users.ConfirmEmailChange;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bookify.Application.UnitTests.Users.Events;

public class ConfirmEmailChangedDomainEventHandlerTests
{
    private readonly IUserRepository _userRepositoryMock;
    private readonly IEmailService _emailServiceMock;
    private readonly IEmailTemplateService _emailTemplateServiceMock;
    private readonly ConfirmEmailChangedDomainEventHandler _handler;

    public ConfirmEmailChangedDomainEventHandlerTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _emailServiceMock = Substitute.For<IEmailService>();
        _emailTemplateServiceMock = Substitute.For<IEmailTemplateService>();

        _handler = new ConfirmEmailChangedDomainEventHandler(
            _userRepositoryMock,
            _emailServiceMock,
            _emailTemplateServiceMock);
    }

    [Fact]
    public async Task Handle_Should_DoNothing_WhenUserNotFound()
    {
        // Arrange
        var domainEvent = new UserEmailChangedDomainEvent(Guid.NewGuid(), "old@test.com", "new@test.com");
        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _emailTemplateServiceMock.DidNotReceive().GenerateEmailBodyAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
        await _emailServiceMock.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_SendTwoEmails_WhenUserFound()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("new@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        var domainEvent = new UserEmailChangedDomainEvent(user.Id, "old@test.com", "new@test.com");

        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        _emailTemplateServiceMock.GenerateEmailBodyAsync("EmailChanged.html", Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns("Body for old email");

        _emailTemplateServiceMock.GenerateEmailBodyAsync("EmailChangedSuccess.html", Arg.Any<object>(), Arg.Any<CancellationToken>())
           .Returns("Body for new email");

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        // Verify notification to OLD email
        await _emailServiceMock.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.To == "old@test.com" && m.Subject == "Security Alert: Email Address Changed" && m.Body == "Body for old email"),
            Arg.Any<CancellationToken>());

        // Verify notification to NEW email
        await _emailServiceMock.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.To == "new@test.com" && m.Subject == "Email Change Successful" && m.Body == "Body for new email"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenEmailServiceFails()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("new@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        var domainEvent = new UserEmailChangedDomainEvent(user.Id, "old@test.com", "new@test.com");

        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        _emailTemplateServiceMock.GenerateEmailBodyAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns("Body");

        _emailServiceMock.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Email service failed"));

        // Act
        Func<Task> act = async () => await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Email service failed");
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenTemplateNotFound()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("new@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        var domainEvent = new UserEmailChangedDomainEvent(user.Id, "old@test.com", "new@test.com");

        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        _emailTemplateServiceMock.GenerateEmailBodyAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new FileNotFoundException("Template not found"));

        // Act
        Func<Task> act = async () => await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>()
             .WithMessage("Template not found");
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenTemplateHasErrors()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("new@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)), Role.Guest);
        var domainEvent = new UserEmailChangedDomainEvent(user.Id, "old@test.com", "new@test.com");

        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        _emailTemplateServiceMock.GenerateEmailBodyAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Template error"));

        // Act
        Func<Task> act = async () => await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Template error");
    }
}
