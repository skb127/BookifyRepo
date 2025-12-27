using Asp.Versioning;
using Bookify.Application.Users;
using Bookify.Application.Users.GetLoggedInUser;
using Bookify.Application.Users.LoginUser;
using Bookify.Application.Users.LogoutUser;
using Bookify.Application.Users.RefreshTokenUser;
using Bookify.Application.Users.RegisterUser;
using Bookify.Domain.Abstractions;
using Bookify.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookify.Api.Controllers.Users;

[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route("api/v{version:apiVersion}/users")]
public sealed class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender) => 
        _sender = sender;

    [HttpGet("me")]
    //[MapToApiVersion(ApiVersions.V1)]
    //[Authorize(Roles = Roles.Registered)] // Role-based
    [HasPermission(Permissions.UsersRead)] // Permission-based
    public async Task<IActionResult> GetLoggedInUser(CancellationToken cancellationToken)
    {
        var query = new GetLoggedInUserQuery();

        Result<UserResponse> result = await _sender.Send(query, cancellationToken);

        return Ok(result.Value);
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(
            request.Email,
            request.FirstName,
            request.LastName,
            request.Password);

        Result<Guid> result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new LoginUserCommand(
            request.Email,
            request.Password);

        Result<AccessTokenResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return Unauthorized(result.Error);
        }

        // Set the new refresh token in the cookie
        SetRefreshTokenCookie(result.Value);

        return Ok(new AccessTokenOnlyResponse(result.Value.AccessToken));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue("refreshToken", out string? refreshToken) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized();
        }

        var command = new LogoutUserCommand(refreshToken);

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return Unauthorized(result.Error);
        }
        
        // Delete the refresh token cookie
        Response.Cookies.Delete("refreshToken");
        
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue("refreshToken", out string? refreshToken) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized();
        }

        var command = new RefreshTokenUserCommand(refreshToken);

        Result<AccessTokenResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return Unauthorized(result.Error);
        }

        // Set the new refresh token in the cookie
        SetRefreshTokenCookie(result.Value);

        return Ok(new AccessTokenOnlyResponse(result.Value.AccessToken));
    }

    private void SetRefreshTokenCookie(AccessTokenResponse accessTokenResponse)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddSeconds(accessTokenResponse.RefreshExpiresIn)
        };

        Response.Cookies.Append("refreshToken", accessTokenResponse.RefreshToken, cookieOptions);
    }
}
