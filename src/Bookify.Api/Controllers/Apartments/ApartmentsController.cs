using Asp.Versioning;
using Bookify.Application.Apartments.SearchApartments;
using Bookify.Application.Bookings.GetBookings;
using Bookify.Application.Bookings.GetPriceEstimate;
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

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetApartment(Guid id, CancellationToken cancellationToken)
    {
        var query = new Application.Apartments.GetApartment.GetApartmentQuery(id);
        Result<Application.Apartments.GetApartment.ApartmentResponse> result = await _sender.Send(query, cancellationToken);

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

    [HttpGet]
    public async Task<IActionResult> SearchApartments(
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] string? city,
        [FromQuery] string? country,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? currency,
        [FromQuery] int[]? amenities,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new SearchApartmentsQuery(
            startDate,
            endDate,
            city,
            country,
            minPrice,
            maxPrice,
            currency,
            amenities,
            page,
            pageSize);

        Result<PagedResponse<ApartmentResponse>> result = await _sender.Send(query, cancellationToken);

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

        return CreatedAtAction(nameof(GetApartment), new { id = result.Value }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.ApartmentsWrite)]
    public async Task<IActionResult> UpdateApartment(
        Guid id,
        UpdateApartmentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new Application.Apartments.UpdateApartment.UpdateApartmentCommand(
            id,
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

        Result result = await _sender.Send(command, cancellationToken);

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

        return NoContent();
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

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.ApartmentsWrite)]
    public async Task<IActionResult> DeleteApartment(Guid id, CancellationToken cancellationToken)
    {
        var command = new Application.Apartments.DeleteApartment.DeleteApartmentCommand(id);
        Result result = await _sender.Send(command, cancellationToken);

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

        return NoContent();
    }

    [HttpGet("{apartmentId:guid}/reviews")]
    [ProducesResponseType(typeof(Application.Reviews.GetApartmentReviews.ApartmentReviewsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApartmentReviews(
        Guid apartmentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new Application.Reviews.GetApartmentReviews.GetApartmentReviewsQuery(apartmentId, page, pageSize);

        Result<Application.Reviews.GetApartmentReviews.ApartmentReviewsResponse> result = await _sender.Send(query, cancellationToken);

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

    [HttpGet("{id:guid}/price-estimate")]
    [ProducesResponseType(typeof(PriceEstimateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPriceEstimate(
        Guid id,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var query = new GetPriceEstimateQuery(id, startDate, endDate);

        Result<PriceEstimateResponse> result = await _sender.Send(query, cancellationToken);

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
