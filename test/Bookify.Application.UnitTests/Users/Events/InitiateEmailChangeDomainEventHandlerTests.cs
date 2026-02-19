using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Options;
using Bookify.Application.Users.InitiateEmailChange;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bookify.Application.UnitTests.Users.Events;

public class InitiateEmailChangeDomainEventHandlerTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private const string Token = "test-token-123";
    private const string NewEmail = "new@test.com";
    private const string CurrentEmail = "current@test.com";

    private static readonly UserEmailChangeInitiatedDomainEvent DomainEvent = new(
        UserId,
        Token,
        NewEmail,
        CurrentEmail);

    private readonly InitiateEmailChangeDomainEventHandler _handler;

    private readonly IUserRepository _userRepositoryMock;
    private readonly IEmailService _emailServiceMock;
    private readonly IEmailTemplateService _emailTemplateServiceMock;

    public InitiateEmailChangeDomainEventHandlerTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _emailServiceMock = Substitute.For<IEmailService>();
        _emailTemplateServiceMock = Substitute.For<IEmailTemplateService>();

        var appOptions = Microsoft.Extensions.Options.Options.Create(new BookifyAppOptions
        {
            FrontendUrl = new Uri("https://localhost:5001")
        });

        // Default template responses
        _emailTemplateServiceMock
            .GenerateEmailBodyAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns("Generated email body");

        _handler = new InitiateEmailChangeDomainEventHandler(
            _userRepositoryMock,
            _emailServiceMock,
            _emailTemplateServiceMock,
            appOptions);
    }

    [Fact]
    public async Task Handle_ShouldNotSendEmails_WhenUserNotFound()
    {
        // Arrange
        _userRepositoryMock
            .GetByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        await _handler.Handle(DomainEvent, CancellationToken.None);

        // Assert
        await _emailServiceMock
            .DidNotReceive()
            .SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSendVerificationEmail_ToNewEmail_WhenUserExists()
    {
        // Arrange
        SetupUserExists();

        // Act
        await _handler.Handle(DomainEvent, CancellationToken.None);

        // Assert
        await _emailServiceMock
            .Received(1)
            .SendAsync(
                Arg.Is<EmailMessage>(m => m.To == NewEmail && m.Subject.Contains("Confirm")),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSendSecurityAlertEmail_ToCurrentEmail_WhenUserExists()
    {
        // Arrange
        SetupUserExists();

        // Act
        await _handler.Handle(DomainEvent, CancellationToken.None);

        // Assert
        await _emailServiceMock
            .Received(1)
            .SendAsync(
                Arg.Is<EmailMessage>(m => m.To == CurrentEmail && m.Subject.Contains("Security Alert")),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSendBothEmails_WhenUserExists()
    {
        // Arrange
        SetupUserExists();

        // Act
        await _handler.Handle(DomainEvent, CancellationToken.None);

        // Assert - Should send exactly 2 emails
        await _emailServiceMock
            .Received(2)
            .SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldUseCorrectTemplate_ForVerificationEmail()
    {
        // Arrange
        SetupUserExists();

        // Act
        await _handler.Handle(DomainEvent, CancellationToken.None);

        // Assert
        await _emailTemplateServiceMock
            .Received(1)
            .GenerateEmailBodyAsync(
                "EmailChangeVerification.html",
                Arg.Any<object>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldUseCorrectTemplate_ForAlertEmail()
    {
        // Arrange
        SetupUserExists();

        // Act
        await _handler.Handle(DomainEvent, CancellationToken.None);

        // Assert
        await _emailTemplateServiceMock
            .Received(1)
            .GenerateEmailBodyAsync(
                "EmailChangeAlert.html",
                Arg.Any<object>(),
                Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task Handle_ShouldThrowException_WhenEmailServiceFails()
    {
        // Arrange
        SetupUserExists();

        _emailServiceMock.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Email service failed"));

        // Act
        Func<Task> act = async () => await _handler.Handle(DomainEvent, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Email service failed");
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenTemplateNotFound()
    {
        // Arrange
        SetupUserExists();

        _emailTemplateServiceMock.GenerateEmailBodyAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new FileNotFoundException("Template not found"));

        // Act
        Func<Task> act = async () => await _handler.Handle(DomainEvent, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>()
            .WithMessage("Template not found");
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenTemplateHasErrors()
    {
        // Arrange
        SetupUserExists();

        _emailTemplateServiceMock.GenerateEmailBodyAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Template error"));

        // Act
        Func<Task> act = async () => await _handler.Handle(DomainEvent, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Template error");
    }

    private void SetupUserExists()
    {
        User user = UserData.Create();

        _userRepositoryMock
            .GetByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(user);
    }
}
