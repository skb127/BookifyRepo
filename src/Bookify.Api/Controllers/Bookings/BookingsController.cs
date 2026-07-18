using Asp.Versioning;
using Bookify.Application.Bookings.CancelBooking;
using Bookify.Application.Bookings.CheckInBooking;
using Bookify.Application.Bookings.CheckOutBooking;
using Bookify.Application.Bookings.CloseStay;
using Bookify.Application.Bookings.CompleteBooking;
using Bookify.Application.Bookings.ConfirmBooking;
using Bookify.Application.Bookings.GetBooking;
using Bookify.Application.Bookings.GetBookings;
using Bookify.Application.Bookings.GetUserBookings;
using Bookify.Application.Bookings.MarkNoShowBooking;
using Bookify.Application.Bookings.RejectBooking;
using Bookify.Application.Bookings.ReserveBooking;
using Bookify.Application.Bookings.GetBookingTransactions;
using Bookify.Application.Bookings.GetCancellationPreview;
using Bookify.Application.Bookings.GetPriceEstimate;
using Bookify.Application.Common;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Bookify.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Bookify.Api.Filters.Idempotency;
using Bookify.Domain.Apartments;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bookify.Api.Controllers.Bookings;

[Authorize]
[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route("api/v{version:apiVersion}/bookings")]
public sealed class BookingsController : ControllerBase
{
    private readonly ISender _sender;

    public BookingsController(ISender sender) =>
        _sender = sender;

    [HasPermission(Permissions.BookingsRead)]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<BookingSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBookings(
        [FromQuery] Guid? userId,
        [FromQuery] Guid? apartmentId,
        [FromQuery] int? status,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetBookingsQuery(userId, apartmentId, status, startDate, endDate, page, pageSize);

        Result<PagedResponse<BookingSummaryResponse>> result = await _sender.Send(query, cancellationToken);

        return Ok(result.Value);
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(PagedResponse<UserBookingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyBookings(
        [FromQuery] int? status,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetUserBookingsQuery(status, startDate, endDate, page, pageSize);

        Result<PagedResponse<UserBookingResponse>> result = await _sender.Send(query, cancellationToken);

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetBooking(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetBookingQuery(id);

        Result<BookingResponse> result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    [Idempotent(cacheTimeInMinutes: 60)]
    [EnableRateLimiting("write-operations")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> ReserveBooking(
        ReserveBookingRequest request,
        CancellationToken cancellation)
    {
        var command = new ReserveBookingCommand(
            request.ApartmentId,
            request.StartDate,
            request.EndDate,
            request.GuestCount);

        Result<Guid> result = await _sender.Send(command, cancellation);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return CreatedAtAction(
            nameof(GetBooking),
            new
            {
                id = result.Value
            },
            result.Value);
    }

    [HttpPut("{id:guid}/confirmation")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> ConfirmBooking(
        Guid id,
        CancellationToken cancellation)
    {
        var command = new ConfirmBookingCommand(id);

        Result result = await _sender.Send(command, cancellation);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return NoContent();
    }

    [HttpPut("{id:guid}/cancellation")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> CancelBooking(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
        BookingReasonRequest? request,
        CancellationToken cancellationToken)
    {
        var command = new CancelBookingCommand(id, request?.Type, request?.Description);

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == BookingErrors.NotFound)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    detail: result.Error.Name,
                    title: result.Error.Code);
            }

            if (result.Error == BookingErrors.Unauthorized)
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

    [HttpPut("{id:guid}/rejection")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> RejectBooking(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
        BookingReasonRequest? request,
        CancellationToken cancellationToken)
    {
        var command = new RejectBookingCommand(id, request?.Type, request?.Description);

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == BookingErrors.Unauthorized)
            {
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    detail: result.Error.Name,
                    title: result.Error.Code);
            }

            if (result.Error == BookingErrors.NotFound)
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

        return NoContent();
    }

    [HasPermission(Permissions.BookingsWrite)]
    [HttpPut("{id:guid}/completion")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> CompleteBooking(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new CompleteBookingCommand(id);

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == BookingErrors.NotFound)
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

        return NoContent();
    }

    [HttpPut("{id:guid}/check-in")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> CheckInBooking(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
        CheckInBookingRequest? request,
        CancellationToken cancellationToken)
    {
        var command = new CheckInBookingCommand(id, request?.GuestCheckInDate);

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == BookingErrors.NotFound)
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

        return NoContent();
    }

    [HttpPut("{id:guid}/check-out")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> CheckOutBooking(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
        CheckOutBookingRequest? request,
        CancellationToken cancellationToken)
    {
        var command = new CheckOutBookingCommand(id, request?.Type, request?.Description, request?.GuestCheckOutDate);

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == BookingErrors.NotFound)
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

        return NoContent();
    }

    [HasPermission(Permissions.BookingsWrite)]
    [HttpPut("{id:guid}/close-stay")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> CloseStay(
        Guid id,
        [FromBody] CloseStayRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CloseStayCommand(id, request.CheckInDate ?? default, request.CheckOutDate ?? default);

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == BookingErrors.NotFound)
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

        return NoContent();
    }

    [HttpPut("{id:guid}/no-show")]
    [EnableRateLimiting("write-operations")]
    public async Task<IActionResult> MarkNoShowBooking(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new MarkNoShowBookingCommand(id);

        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == BookingErrors.NotFound)
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

        return NoContent();
    }

    [HttpGet("price-estimate")]
    public async Task<IActionResult> GetPriceEstimate(
        [FromQuery] Guid apartmentId,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        [FromQuery] int guestCount = 1,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPriceEstimateQuery(
            apartmentId, startDate, endDate, guestCount);

        Result<PriceEstimateResponse> result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == ApartmentErrors.NotFound)
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

    [HttpGet("{id:guid}/cancellation-preview")]
    public async Task<IActionResult> GetCancellationPreview(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetCancellationPreviewQuery(id);

        Result<CancellationPreviewResponse> result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == BookingErrors.NotFound)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    detail: result.Error.Name,
                    title: result.Error.Code);
            }

            if (result.Error == BookingErrors.Unauthorized)
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

        return Ok(result.Value);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpGet("{id:guid}/transactions")]
    public async Task<IActionResult> GetBookingTransactions(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetBookingTransactionsQuery(id);

        Result<IReadOnlyList<BookingTransactionResponse>> result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok(result.Value);
    }
}
