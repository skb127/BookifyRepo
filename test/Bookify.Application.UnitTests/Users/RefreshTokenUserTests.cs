using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Users;
using Bookify.Application.Users.RefreshTokenUser;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;
using FluentAssertions;
using NSubstitute;

namespace Bookify.Application.UnitTests.Users;

public class RefreshTokenUserTests
{
    private static readonly RefreshTokenUserCommand Command = new("RefreshToken");

    private readonly IJwtService _jwtServiceMock;
    private readonly RefreshTokenUserCommandHandler _handler;

    public RefreshTokenUserTests()
    {
        _jwtServiceMock = Substitute.For<IJwtService>();
        _handler = new RefreshTokenUserCommandHandler(_jwtServiceMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnToken_WhenRefreshTokenIsValid()
    {
        // Arrange
        var accessTokenResponse = new AccessTokenResponse("AccessToken", "RefreshToken", 3600, 3600);
        _jwtServiceMock.GetRefreshTokenAsync(Command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns(Result.Success(accessTokenResponse));

        // Act
        var result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(accessTokenResponse);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenRefreshTokenIsInvalid()
    {
        // Arrange
        _jwtServiceMock.GetRefreshTokenAsync(Command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AccessTokenResponse>(new Error("Auth.Failed", "Failed")));

        // Act
        var result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidCredentials);
    }
}
