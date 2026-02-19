using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Options;
using Bookify.Application.Users.PasswordRecovery;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bookify.Application.UnitTests.Users.Events;

public class PasswordRecoveryDomainEventHandlerTests
{
    private readonly IUserRepository _userRepositoryMock;
    private readonly IEmailService _emailServiceMock;
    private readonly IEmailTemplateService _emailTemplateServiceMock;
    private readonly IOptions<BookifyAppOptions> _appOptionsMock;
    private readonly PasswordRecoveryDomainEventHandler _handler;

    public PasswordRecoveryDomainEventHandlerTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _emailServiceMock = Substitute.For<IEmailService>();
        _emailTemplateServiceMock = Substitute.For<IEmailTemplateService>();
        _appOptionsMock = Substitute.For<IOptions<BookifyAppOptions>>();
        _appOptionsMock.Value.Returns(new BookifyAppOptions { FrontendUrl = new Uri("http://localhost:3000") });

        _handler = new PasswordRecoveryDomainEventHandler(
            _userRepositoryMock,
            _emailServiceMock,
            _emailTemplateServiceMock,
            _appOptionsMock);
    }

    [Fact]
    public async Task Handle_Should_DoNothing_WhenUserNotFound()
    {
        // Arrange
        var domainEvent = new UserPasswordRecoveryDomainEvent(Guid.NewGuid(), "some-token");
        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _emailTemplateServiceMock.DidNotReceive().GenerateEmailBodyAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
        await _emailServiceMock.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_SendEmail_WhenUserFound()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var domainEvent = new UserPasswordRecoveryDomainEvent(user.Id, "recovery-token");

        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        _emailTemplateServiceMock.GenerateEmailBodyAsync("PasswordRecoveryRequest.html", Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns("Recovery Email Body");

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _emailServiceMock.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.To == user.Email.Value && m.Subject == "Password Recovery Request" && m.Body == "Recovery Email Body"),
            Arg.Any<CancellationToken>());

        await _emailTemplateServiceMock.Received(1).GenerateEmailBodyAsync(
            "PasswordRecoveryRequest.html",
            Arg.Is<object>(o => o.GetType().GetProperty("ResetPasswordUrl")!.GetValue(o, null)!.ToString()!.Contains("recovery-token")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenEmailServiceFails()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var domainEvent = new UserPasswordRecoveryDomainEvent(user.Id, "recovery-token");

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
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var domainEvent = new UserPasswordRecoveryDomainEvent(user.Id, "recovery-token");

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
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var domainEvent = new UserPasswordRecoveryDomainEvent(user.Id, "recovery-token");

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
