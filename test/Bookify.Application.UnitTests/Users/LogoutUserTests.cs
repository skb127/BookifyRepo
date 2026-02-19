using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Users.LogoutUser;
using Bookify.Domain.Abstractions;
using FluentAssertions;
using NSubstitute;

namespace Bookify.Application.UnitTests.Users;

public class LogoutUserTests
{
    private static readonly LogoutUserCommand Command = new("RefreshToken");

    private readonly IJwtService _jwtServiceMock;
    private readonly LogoutUserCommandHandler _handler;

    public LogoutUserTests()
    {
        _jwtServiceMock = Substitute.For<IJwtService>();
        _handler = new LogoutUserCommandHandler(_jwtServiceMock);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenRevokeSucceeds()
    {
        // Arrange
        _jwtServiceMock.RevokeUserAsync(Command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenRevokeFails()
    {
        // Arrange
        var error = new Error("Auth.InvalidToken", "Token is invalid");
        _jwtServiceMock.RevokeUserAsync(Command.RefreshToken, Arg.Any<CancellationToken>())
            .Returns(Result.Failure(error));

        // Act
        var result = await _handler.Handle(Command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }
}
