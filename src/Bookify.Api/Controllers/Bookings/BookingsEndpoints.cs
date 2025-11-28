using Bookify.Application.Bookings.ConfirmBooking;
using Bookify.Application.Bookings.GetBooking;
using Bookify.Application.Bookings.ReserveBooking;
using Bookify.Domain.Abstractions;
using MediatR;

namespace Bookify.Api.Controllers.Bookings;

public static class BookingsEndpoints
{
    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder builder)
    {
        // Get booking by id
        builder.MapGet("bookings/{id:guid}", GetBooking)
            .RequireAuthorization()
            .WithName(nameof(GetBooking));

        // Reserve booking
        builder.MapPost("bookings/", ReserveBooking)
            .RequireAuthorization();

        // Confirm booking
        builder.MapPost("bookings/{id:guid}/confirmation", ReserveBooking)
            .RequireAuthorization();

        return builder;
    }

    public static async Task<IResult> GetBooking(
        Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetBookingQuery(id);

        Result<BookingResponse> result = await sender.Send(query, cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
    }

    public static async Task<IResult> ReserveBooking(
        ReserveBookingRequest request,
        ISender sender,
        CancellationToken cancellation)
    {
        var command = new ReserveBookingCommand(
            request.ApartmentId,
            request.UserId,
            request.StartDate,
            request.EndDate);

        Result<Guid> result = await sender.Send(command, cancellation);

        if (result.IsFailure)
        {
            return Results.BadRequest(result.Error);
        }

        return Results.CreatedAtRoute(
            nameof(GetBooking),
            new
            {
                id = result.Value
            },
            result.Value);
    }

    public static async Task<IResult> ConfirmBooking(
        Guid id,
        ISender sender,
        CancellationToken cancellation)
    {
        var command = new ConfirmBookingCommand(id);

        Result result = await sender.Send(command, cancellation);

        if (result.IsFailure)
        {
            return Results.BadRequest(result.Error);
        }

        return Results.NoContent();
    }
}
