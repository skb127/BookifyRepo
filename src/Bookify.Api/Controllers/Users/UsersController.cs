using Asp.Versioning;
using Bookify.Api.Filters.Turnstile;
using Bookify.Application.Users;
using Bookify.Application.Users.AdminDeleteUser;
using Bookify.Application.Users.BanUser;
using Bookify.Application.Users.CancelAccountDeletion;
using Bookify.Application.Users.ChangePasswordUser;
using Bookify.Application.Users.ConfirmEmailChange;
using Bookify.Application.Users.GetLoggedInUser;
using Bookify.Application.Users.GetUserById;
using Bookify.Application.Users.InitiateEmailChange;
using Bookify.Application.Users.LoginUser;
using Bookify.Application.Users.LogoutUser;
using Bookify.Application.Users.PasswordRecovery;
using Bookify.Application.Users.PasswordReset;
using Bookify.Application.Users.RefreshTokenUser;
using Bookify.Application.Users.RegisterGuest;
using Bookify.Application.Users.RegisterHost;
using Bookify.Application.Users.RequestAccountDeletion;
using Bookify.Application.Users.RevokeAllSessions;
using Bookify.Application.Users.UnbanUser;
using Bookify.Application.Users.UpdateUserProfile;
using Bookify.Domain.Abstractions;
using Bookify.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bookify.Api.Controllers.Users;

[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route("api/v{version:apiVersion}/users")]
public sealed class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender) =>
        _sender = sender;

    [AllowAnonymous]
    [Turnstile]
    [HttpPost("login")]
    [EnableRateLimiting("write-operations")]
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
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        // Set the new refresh token in the cookie
        SetRefreshTokenCookie(result.Value);

        return Ok(new AccessTokenOnlyResponse(result.Value.AccessToken));
    }

    [Authorize]
    [HttpPost("refresh")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue("refreshToken", out string? refreshToken))
        {
            return Unauthorized();
        }

        var command = new RefreshTokenUserCommand(refreshToken);

        Result<AccessTokenResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return Unauthorized();
        }

        // Set the new refresh token in the cookie
        SetRefreshTokenCookie(result.Value);

        return Ok(new AccessTokenOnlyResponse(result.Value.AccessToken));
    }

    [Authorize]
    [HttpPost("logout")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue("refreshToken", out string? refreshToken))
        {
            return NoContent();
        }

        var command = new LogoutUserCommand(refreshToken);

        await _sender.Send(command, cancellationToken);

        Response.Cookies.Delete("refreshToken");

        return NoContent();
    }

#pragma warning disable S1133
    [Obsolete("This endpoint is deprecated. Use /register/guest or /register/host instead.")]
    [HttpPost("register")]
    public IActionResult RegisterOld() =>
        StatusCode(StatusCodes.Status410Gone, new ProblemDetails
        {
            Status = StatusCodes.Status410Gone,
            Title = "Endpoint Deprecated",
            Detail = "This registration endpoint is no longer supported. Please use /register/guest or /register/host."
        });
#pragma warning restore S1133

    [AllowAnonymous]
    [Turnstile]
    [HttpPost("register/guest")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> RegisterGuest(
        RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterGuestCommand(
            request.Email,
            request.FirstName,
            request.LastName,
            request.Password,
            request.DateOfBirth);

        Result<Guid> result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok(result.Value);
    }

    [AllowAnonymous]
    [Turnstile]
    [HttpPost("register/host")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> RegisterHost(
        RegisterHostRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterHostCommand(
            request.Email,
            request.FirstName,
            request.LastName,
            request.Password,
            request.DateOfBirth,
            request.PhoneNumber);

        Result<Guid> result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok(result.Value);
    }

    [HasPermission(Permissions.UsersAdminRead)]
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetUserById(Guid userId, CancellationToken cancellationToken)
    {
        var query = new GetUserByIdQuery(userId);

        Result<AdminUserResponse> result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound();
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpPost("revoke-all-sessions")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> RevokeAllSessions(CancellationToken cancellationToken)
    {
        var command = new RevokeAllSessionsCommand();

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        Response.Cookies.Delete("refreshToken");

        return NoContent();
    }

    [HttpGet("me")]
    //[MapToApiVersion(ApiVersions.V1)]
    //[Authorize(Roles = Roles.Registered)] // Role-based
    [HasPermission(Permissions.UsersRead)] // Permission-based
    public async Task<IActionResult> GetLoggedInUser(CancellationToken cancellationToken)
    {
        var query = new GetLoggedInUserQuery();

        Result<UserResponse> result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpPut("profile")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> UpdateProfile(
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateUserProfileCommand(
            request.FirstName,
            request.LastName,
            request.PhoneNumber,
            request.DateOfBirth,
            request.Password);

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok();
    }

    [Authorize]
    [HttpPost("change-password")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> ChangePassword(
        ChangeUserPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ChangePasswordUserCommand(
            request.CurrentPassword,
            request.NewPassword);

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok();
    }

    [AllowAnonymous]
    [Turnstile]
    [HttpPost("forgot-password")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> ForgotPassword(
        PasswordRecoveryRequest recoveryRequest,
        CancellationToken cancellationToken)
    {
        var command = new PasswordRecoveryCommand(recoveryRequest.Email);

        _ = await _sender.Send(command, cancellationToken);

        return Ok();
    }

    [AllowAnonymous]
    [Turnstile]
    [HttpPost("reset-password")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> ResetPassword(
        PasswordResetRequest resetRequest,
        CancellationToken cancellationToken)
    {
        var command = new PasswordResetCommand(resetRequest.Token, resetRequest.NewPassword);

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok();
    }

    [Authorize]
    [HttpPost("change-email")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> InitiateEmailChange(
        InitiateEmailChangeRequest request,
        CancellationToken cancellationToken)
    {
        var command = new InitiateEmailChangeCommand(
            request.NewEmail,
            request.CurrentPassword);

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok();
    }

    [AllowAnonymous]
    [HttpPost("confirm-email-change")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> ConfirmEmailChange(
        ConfirmEmailChangeRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ConfirmEmailChangeCommand(request.Token);

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Content(
            "<html><body><h1>Success</h1><p>Your email has been successfully changed! You can now login with your new email.</p></body></html>",
            "text/html");
    }

    [Authorize]
    [HttpPost("deactivate")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> RequestDeactivation(CancellationToken cancellationToken)
    {
        var command = new RequestAccountDeletionCommand();

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        Response.Cookies.Delete("refreshToken");

        return Ok();
    }

    [AllowAnonymous]
    [HttpPost("cancel-deactivation")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> CancelDeactivation(
        CancelAccountDeletionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CancelAccountDeletionCommand(request.Token);

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok();
    }

    [HasPermission(Permissions.UsersBan)]
    [HttpPost("{userId:guid}/ban")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> BanUser(Guid userId, CancellationToken cancellationToken)
    {
        var command = new BanUserCommand(userId);

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok();
    }

    [HasPermission(Permissions.UsersBan)]
    [HttpPost("{userId:guid}/unban")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> UnbanUser(Guid userId, CancellationToken cancellationToken)
    {
        var command = new UnbanUserCommand(userId);

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok();
    }

    [HasPermission(Permissions.UsersAdminWrite)]
    [HttpDelete("{userId:guid}")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> AdminDeleteUser(Guid userId, CancellationToken cancellationToken)
    {
        var command = new AdminDeleteUserCommand(userId);

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok();
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
