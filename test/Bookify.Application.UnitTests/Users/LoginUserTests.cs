using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Users;
using Bookify.Application.Users.LoginUser;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Users;
using FluentAssertions;
using NSubstitute;

namespace Bookify.Application.UnitTests.Users;

public class LoginUserTests
{
    private static readonly LoginUserCommand Command = new("test@test.com", "Password");

    private readonly IJwtService _jwtServiceMock;
    private readonly LoginUserCommandHandler _handler;

    public LoginUserTests()
    {
        _jwtServiceMock = Substitute.For<IJwtService>();
        _handler = new LoginUserCommandHandler(_jwtServiceMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnToken_WhenCredentialsAreValid()
    {
        // Arrange
        var accessTokenResponse = new AccessTokenResponse("AccessToken", "RefreshToken", 3600, 3600);
        _jwtServiceMock.GetAccessTokenAsync(Command.Email, Command.Password, Arg.Any<CancellationToken>())
            .Returns(Result.Success(accessTokenResponse));

        // Act
        var result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(accessTokenResponse);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenCredentialsAreInvalid()
    {
        // Arrange
        _jwtServiceMock.GetAccessTokenAsync(Command.Email, Command.Password, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AccessTokenResponse>(new Error("Auth.Failed", "Failed")));

        // Act
        var result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidCredentials);
    }
}
