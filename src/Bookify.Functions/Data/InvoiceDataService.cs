using Bookify.Functions.Models;
using Bookify.Functions.Options;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Bookify.Functions.Data;

internal sealed class InvoiceDataService(IOptions<DatabaseOptions> options) : IInvoiceDataService
{
    private readonly DatabaseOptions _options = options.Value;

    public async Task<InvoiceDocumentData?> GetInvoiceDocumentDataAsync(Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT 
                               i.id AS InvoiceId,
                               i.invoice_number AS InvoiceNumber,
                               i.invoice_type AS InvoiceType,
                               i.booking_id AS BookingId,
                               i.issue_date AS IssueDate,
                               i.total_amount AS TotalAmount,
                               i.tax_amount AS TaxAmount,
                               i.currency AS Currency,
                               u.first_name AS GuestFirstName,
                               u.last_name AS GuestLastName,
                               u.email AS GuestEmail,
                               a.name AS ApartmentName,
                               CONCAT_WS(', ', a.address_street, a.address_city, a.address_state, a.address_zip_code, a.address_country) AS ApartmentAddress,
                               b.duration_start AS DurationStart,
                               b.duration_end AS DurationEnd,
                               (b.duration_end - b.duration_start) AS TotalNights,
                               b.price_for_period_amount AS PriceForPeriod,
                               b.cleaning_fee_amount AS CleaningFee,
                               b.amenities_up_charge_amount AS AmenitiesUpCharge,
                               b.extra_guest_charge_amount AS ExtraGuestCharge,
                               b.guest_count AS GuestCount,
                               a.base_guests AS BaseGuests,
                               a.extra_guest_fee_amount AS ExtraGuestFee,
                               orig.invoice_number AS OriginalInvoiceNumber,
                               orig.total_amount AS OriginalTotalAmount,
                               orig.tax_amount AS OriginalTaxAmount,
                               br.amount AS RefundAmount,
                               br.reason AS RefundReason
                           FROM invoices i
                           INNER JOIN bookings b ON b.id = i.booking_id
                           INNER JOIN users u ON u.id = b.user_id
                           INNER JOIN apartments a ON a.id = b.apartment_id
                           LEFT JOIN invoices orig ON orig.id = i.original_invoice_id
                           LEFT JOIN booking_refunds br ON br.booking_id = b.id
                           WHERE i.id = @InvoiceId;

                           SELECT 
                               bt.tax_rule_name AS TaxName,
                               bt.tax_type AS TaxType,
                               bt.rate AS Rate,
                               bt.calculated_amount_amount AS Amount
                           FROM booking_taxes bt
                           WHERE bt.booking_id = (SELECT booking_id FROM invoices WHERE id = @InvoiceId);
                           """;

        await using var connection = new NpgsqlConnection(_options.Database);
        await connection.OpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { InvoiceId = invoiceId }, cancellationToken: cancellationToken);
        await using SqlMapper.GridReader multi = await connection.QueryMultipleAsync(command);

        InvoiceDocumentModel? document = await multi.ReadSingleOrDefaultAsync<InvoiceDocumentModel>();
        if (document is null)
        {
            return null;
        }

        List<TaxLineModel> taxLines = (await multi.ReadAsync<TaxLineModel>()).AsList();
        return new InvoiceDocumentData(document, taxLines);
    }

    public async Task MarkInvoiceAsGeneratedAsync(Guid invoiceId, string pdfBlobName,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE invoices
                           SET status = @Status,
                               pdf_blob_name = @PdfBlobName
                           WHERE id = @InvoiceId
                           """;

        await using var connection = new NpgsqlConnection(_options.Database);
        await connection.OpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new
        {
            InvoiceId = invoiceId,
            PdfBlobName = pdfBlobName,
            Status = (int)InvoiceStatus.Generated
        }, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task MarkInvoiceAsErrorAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE invoices
                           SET status = @Status
                           WHERE id = @InvoiceId
                           """;

        await using var connection = new NpgsqlConnection(_options.Database);
        await connection.OpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new
        {
            InvoiceId = invoiceId,
            Status = (int)InvoiceStatus.Error
        }, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }
}
