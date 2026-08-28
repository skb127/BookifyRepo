using System.Data;
using Bookify.Application.Abstractions.Authentication;
using Bookify.Application.Abstractions.Data;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Abstractions.Storage;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Dapper;

namespace Bookify.Application.Bookings.DownloadInvoice;

internal sealed class DownloadInvoiceQueryHandler : IQueryHandler<DownloadInvoiceQuery, Uri>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    private readonly IUserContext _userContext;
    private readonly IInvoiceFileService _invoiceFileService;

    public DownloadInvoiceQueryHandler(
        ISqlConnectionFactory sqlConnectionFactory,
        IUserContext userContext,
        IInvoiceFileService invoiceFileService)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
        _userContext = userContext;
        _invoiceFileService = invoiceFileService;
    }

    public async Task<Result<Uri>> Handle(DownloadInvoiceQuery request, CancellationToken cancellationToken)
    {
        using IDbConnection connection = _sqlConnectionFactory.CreateConnection();

        const string sql = """
            SELECT user_id AS UserId
            FROM bookings
            WHERE id = @BookingId;

            SELECT
                pdf_blob_name AS PdfBlobName,
                status AS Status
            FROM invoices
            WHERE id = @InvoiceId AND booking_id = @BookingId;
            """;

        using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(
            sql,
            new
            {
                request.BookingId,
                request.InvoiceId
            });

        Guid? bookingUserId = await multi.ReadFirstOrDefaultAsync<Guid?>();
        if (bookingUserId is null)
        {
            return Result.Failure<Uri>(BookingErrors.NotFound);
        }

        if (bookingUserId.Value != _userContext.UserId)
        {
            return Result.Failure<Uri>(BookingErrors.Unauthorized);
        }

        InvoiceRecord? invoice = await multi.ReadFirstOrDefaultAsync<InvoiceRecord>();
        if (invoice is null || string.IsNullOrWhiteSpace(invoice.PdfBlobName) || invoice.Status != (int)InvoiceStatus.Generated)
        {
            return Result.Failure<Uri>(InvoiceErrors.NotFound);
        }

        return _invoiceFileService.GenerateDownloadUrl(invoice.PdfBlobName);
    }

    private sealed record InvoiceRecord(string? PdfBlobName, int Status);
}
