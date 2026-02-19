using Bookify.Application.Abstractions.Email;
using Bookify.Application.Abstractions.Email.Models;
using Bookify.Application.Options;
using Bookify.Application.Users.RegisterUser;
using Bookify.Domain.Users;
using Bookify.Domain.Users.Events;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "Unit tests")]

namespace Bookify.Application.UnitTests.Users.Events;

public class RegisterUserDomainEventHandlerTests
{
    private readonly IUserRepository _userRepositoryMock;
    private readonly IEmailService _emailServiceMock;
    private readonly IEmailTemplateService _emailTemplateServiceMock;
    private readonly IOptions<BookifyAppOptions> _appOptionsMock;
    private readonly RegisterUserDomainEventHandler _handler;

    public RegisterUserDomainEventHandlerTests()
    {
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _emailServiceMock = Substitute.For<IEmailService>();
        _emailTemplateServiceMock = Substitute.For<IEmailTemplateService>();
        _appOptionsMock = Substitute.For<IOptions<BookifyAppOptions>>();
        _appOptionsMock.Value.Returns(new BookifyAppOptions { FrontendUrl = new Uri("http://localhost:3000") });

        _handler = new RegisterUserDomainEventHandler(
            _emailServiceMock,
            _emailTemplateServiceMock,
            _userRepositoryMock,
            _appOptionsMock);
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenUserNotFound()
    {
        // Arrange
        var domainEvent = new UserCreatedDomainEvent(Guid.NewGuid());
        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _emailTemplateServiceMock.DidNotReceive().GenerateEmailBodyAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
        await _emailServiceMock.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSendWelcomeEmail_WhenUserFound()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var domainEvent = new UserCreatedDomainEvent(user.Id);

        _userRepositoryMock.GetByIdAsync(domainEvent.UserId, Arg.Any<CancellationToken>())
            .Returns(user);

        _emailTemplateServiceMock.GenerateEmailBodyAsync("WelcomeUser.html", Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns("Welcome Body");

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _emailServiceMock.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.To == "test@test.com" && m.Subject == "Welcome to Bookify" && m.Body == "Welcome Body"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenEmailServiceFails()
    {
        // Arrange
        var user = User.Create(new FirstName("First"), new LastName("Last"), new Email("test@test.com"), DateOfBirth.Create(new DateOnly(2000, 1, 1)));
        var domainEvent = new UserCreatedDomainEvent(user.Id);

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
        var domainEvent = new UserCreatedDomainEvent(user.Id);

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
        var domainEvent = new UserCreatedDomainEvent(user.Id);

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
