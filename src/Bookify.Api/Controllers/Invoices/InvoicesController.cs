using Asp.Versioning;
using Bookify.Application.Bookings.DownloadInvoice;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookify.Api.Controllers.Invoices;

[Authorize]
[ApiController]
[ApiVersion(ApiVersions.V1)]
[Route("api/v{version:apiVersion}/bookings/{bookingId:guid}/invoices")]
public sealed class InvoicesController : ControllerBase
{
    private readonly ISender _sender;

    public InvoicesController(ISender sender) =>
        _sender = sender;

    [HttpGet("{invoiceId:guid}/download")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DownloadInvoice(
        Guid bookingId,
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        var query = new DownloadInvoiceQuery(bookingId, invoiceId);

        Result<Uri> result = await _sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error == BookingErrors.NotFound || result.Error == InvoiceErrors.NotFound)
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

        return Redirect(result.Value.AbsoluteUri);
    }
}
