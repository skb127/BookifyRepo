using Asp.Versioning;
using Bookify.Application.Common;
using Bookify.Application.Reviews.AddReview;
using Bookify.Application.Reviews.GetMyReviews;
using Bookify.Application.Reviews.GetAllReviews;
using Bookify.Domain.Abstractions;
using Bookify.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookify.Api.Controllers.Reviews;

[Authorize]
[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route("api/v{version:apiVersion}/reviews")]
public sealed class ReviewsController : ControllerBase
{
    private readonly ISender _sender;

    public ReviewsController(ISender sender) =>
        _sender = sender;

    [HttpPost]
    public async Task<IActionResult> AddReview(AddReviewRequest request, CancellationToken cancellationToken)
    {
        var command = new AddReviewCommand(request.BookingId, request.Rating, request.Comment);

        Result<Guid> result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return CreatedAtAction(nameof(GetReview), new { id = result.Value }, result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Application.Reviews.GetReview.ReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReview(Guid id, CancellationToken cancellationToken)
    {
        var query = new Application.Reviews.GetReview.GetReviewQuery(id);

        Result<Application.Reviews.GetReview.ReviewResponse> result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == Domain.Reviews.ReviewErrors.NotFound)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    detail: result.Error.Name,
                    title: result.Error.Code);
            }

            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateReview(
        Guid id,
        UpdateReviewRequest request,
        CancellationToken cancellationToken)
    {
        var command = new Application.Reviews.UpdateReview.UpdateReviewCommand(id, request.Rating, request.Comment);

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == Domain.Reviews.ReviewErrors.NotFound)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    detail: result.Error.Name,
                    title: result.Error.Code);
            }

            if (result.Error == Domain.Reviews.ReviewErrors.NotAuthor)
            {
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    detail: result.Error.Name,
                    title: result.Error.Code);
            }

            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return NoContent();
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(PagedResponse<MyReviewResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyReviews(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetMyReviewsQuery(page, pageSize);
        Result<PagedResponse<MyReviewResponse>> result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok(result.Value);
    }

    [HttpGet]
    [HasPermission(Permissions.ReviewsRead)]
    [ProducesResponseType(typeof(PagedResponse<AllReviewsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllReviews(
        [FromQuery] Guid? apartmentId,
        [FromQuery] Guid? userId,
        [FromQuery] int? minRating,
        [FromQuery] int? maxRating,
        [FromQuery] bool? isEdited,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAllReviewsQuery(apartmentId, userId, minRating, maxRating, isEdited, page, pageSize);
        Result<PagedResponse<AllReviewsResponse>> result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteReview(Guid id, CancellationToken cancellationToken)
    {
        var command = new Application.Reviews.DeleteReview.DeleteReviewCommand(id);
        
        Result result = await _sender.Send(command, cancellationToken);

        if (!result.IsFailure)
        {
            return NoContent();
        }

        if (result.Error == Domain.Reviews.ReviewErrors.NotFound)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        if (result.Error == Domain.Reviews.ReviewErrors.NotAuthor)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            detail: result.Error.Name,
            title: result.Error.Code);

    }
}
