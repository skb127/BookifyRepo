using Asp.Versioning;
using Bookify.Application.Apartments.SearchApartments;
using Bookify.Application.Bookings.GetBookings;
using Bookify.Application.Common;
using Bookify.Domain.Abstractions;
using Bookify.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookify.Api.Controllers.Apartments;

[Authorize]
[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route("api/v{version:apiVersion}/apartments")]
public sealed class ApartmentsController : ControllerBase
{
    private readonly ISender _sender;

    public ApartmentsController(ISender sender) =>

        _sender = sender;

    [HttpGet]
    public async Task<IActionResult> SearchApartments(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var query = new SearchApartmentsQuery(startDate, endDate);

        Result<IReadOnlyList<ApartmentResponse>> result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    [HasPermission(Permissions.ApartmentsWrite)]
    public async Task<IActionResult> CreateApartment(
        CreateApartmentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new Application.Apartments.CreateApartment.CreateApartmentCommand(
            request.Name,
            request.Description,
            request.Address.Country,
            request.Address.State,
            request.Address.ZipCode,
            request.Address.City,
            request.Address.Street,
            request.Price.Amount,
            request.Price.Currency,
            request.CleaningFee.Amount,
            request.CleaningFee.Currency,
            request.Amenities);

        Result<Guid> result = await _sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return CreatedAtAction(nameof(SearchApartments), new { id = result.Value }, result.Value);
    }

    [HttpGet("{apartmentId:guid}/bookings")]
    public async Task<IActionResult> GetApartmentBookings(
        Guid apartmentId,
        [FromQuery] int? status,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new Application.Bookings.GetApartmentBookings.GetApartmentBookingsQuery(
            apartmentId, status, startDate, endDate, page, pageSize);

        Result<PagedResponse<BookingSummaryResponse>> result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            int statusCode = StatusCodes.Status400BadRequest;

            if (result.Error == Domain.Bookings.BookingErrors.Unauthorized)
            {
                statusCode = StatusCodes.Status403Forbidden;
            }
            else if (result.Error == Domain.Apartments.ApartmentErrors.NotFound)
            {
                statusCode = StatusCodes.Status404NotFound;
            }

            return Problem(
                statusCode: statusCode,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok(result.Value);
    }

    [HttpGet("{apartmentId:guid}/availability")]
    public async Task<IActionResult> CheckAvailability(
        Guid apartmentId,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var query = new Application.Apartments.CheckApartmentAvailability.CheckApartmentAvailabilityQuery(apartmentId, startDate, endDate);


        Result<Application.Apartments.CheckApartmentAvailability.ApartmentAvailabilityResponse> result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            int statusCode = result.Error == Domain.Apartments.ApartmentErrors.NotFound
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;

            return Problem(
                statusCode: statusCode,
                detail: result.Error.Name,
                title: result.Error.Code);
        }

        return Ok(result.Value);
    }
}
