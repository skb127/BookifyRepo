using Asp.Versioning;
using Bookify.Application.Bookings.CancelBooking;
using Bookify.Application.Bookings.CompleteBooking;
using Bookify.Application.Bookings.ConfirmBooking;
using Bookify.Application.Bookings.GetBooking;
using Bookify.Application.Bookings.GetBookings;
using Bookify.Application.Bookings.GetUserBookings;
using Bookify.Application.Bookings.RejectBooking;
using Bookify.Application.Bookings.ReserveBooking;
using Bookify.Application.Common;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Bookify.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    [HttpGet("admin")]
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

    [HttpGet]
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
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> ReserveBooking(
        ReserveBookingRequest request,
        CancellationToken cancellation)
    {
        var command = new ReserveBookingCommand(
            request.ApartmentId,
            request.StartDate,
            request.EndDate);

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
    public async Task<IActionResult> CancelBooking(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new CancelBookingCommand(id);

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

    [HttpPut("{id:guid}/rejection")]
    public async Task<IActionResult> RejectBooking(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new RejectBookingCommand(id);

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

    [HttpGet("price-estimate")]
    public async Task<IActionResult> GetPriceEstimate(
        [FromQuery] Guid apartmentId,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var query = new Application.Bookings.GetPriceEstimate.GetPriceEstimateQuery(
            apartmentId, startDate, endDate);

        Bookify.Domain.Abstractions.Result<Application.Bookings.GetPriceEstimate.PriceEstimateResponse> result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == Domain.Apartments.ApartmentErrors.NotFound)
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
}
