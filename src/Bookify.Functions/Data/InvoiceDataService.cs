using System.Diagnostics.CodeAnalysis;
using Bookify.Functions.Models;
using Bookify.Functions.Options;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Bookify.Functions.Data;

internal sealed class InvoiceDataService(IOptions<DatabaseOptions> options) : IInvoiceDataService
{
    private readonly DatabaseOptions _options = options.Value;

    public async Task<InvoiceDocumentModel?> GetInvoiceDocumentDataAsync(Guid invoiceId, CancellationToken cancellationToken = default)
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
                (b.duration_end - b.duration_start) AS TotalNights
            FROM invoices i
            INNER JOIN bookings b ON b.id = i.booking_id
            INNER JOIN users u ON u.id = b.user_id
            INNER JOIN apartments a ON a.id = b.apartment_id
            WHERE i.id = @InvoiceId
            """;

        await using var connection = new NpgsqlConnection(_options.Database);
        await connection.OpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { InvoiceId = invoiceId }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<InvoiceDocumentModel>(command);
    }

    [SuppressMessage("Design", "CA1054:Uri parameters should not be strings", Justification = "PDF URLs are stored as strings in the database and handled as strings.")]
    public async Task MarkInvoiceAsGeneratedAsync(Guid invoiceId, string pdfUrl, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE invoices
            SET status = 1,
                pdf_url = @PdfUrl
            WHERE id = @InvoiceId
            """;

        await using var connection = new NpgsqlConnection(_options.Database);
        await connection.OpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { InvoiceId = invoiceId, PdfUrl = pdfUrl }, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task MarkInvoiceAsErrorAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE invoices
            SET status = 2
            WHERE id = @InvoiceId
            """;

        await using var connection = new NpgsqlConnection(_options.Database);
        await connection.OpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { InvoiceId = invoiceId }, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }
}
