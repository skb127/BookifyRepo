using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Identity;
using Bookify.Application.Users.RevokeAllSessions;
using Bookify.Domain.Abstractions;
using FluentAssertions;
using NSubstitute;

namespace Bookify.Application.UnitTests.Users;

public class RevokeAllSessionsTests
{
    private static readonly RevokeAllSessionsCommand Command = new();
    private const string IdentityId = "identity-id-123";

    private readonly IIdentityProvider _identityProviderMock;
    private readonly IUserContext _userContextMock;
    private readonly RevokeAllSessionsCommandHandler _handler;

    public RevokeAllSessionsTests()
    {
        _identityProviderMock = Substitute.For<IIdentityProvider>();
        _userContextMock = Substitute.For<IUserContext>();

        _userContextMock.IdentityId.Returns(IdentityId);

        _handler = new RevokeAllSessionsCommandHandler(
            _identityProviderMock,
            _userContextMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenLogoutAllSessionsSucceeds()
    {
        // Arrange
        _identityProviderMock
            .LogoutAllSessionsAsync(IdentityId, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenLogoutAllSessionsFails()
    {
        // Arrange
        var error = new Error("Keycloak.LogoutFailed", "Failed to logout user sessions");

        _identityProviderMock
            .LogoutAllSessionsAsync(IdentityId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure(error));

        // Act
        var result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public async Task Handle_ShouldCallLogoutAllSessions_WithCorrectIdentityId()
    {
        // Arrange
        _identityProviderMock
            .LogoutAllSessionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        await _handler.Handle(Command, CancellationToken.None);

        // Assert
        await _identityProviderMock.Received(1)
            .LogoutAllSessionsAsync(IdentityId, Arg.Any<CancellationToken>());
    }
}
