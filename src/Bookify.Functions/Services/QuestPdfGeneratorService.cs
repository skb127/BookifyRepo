using System.Globalization;
using Bookify.Functions.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Bookify.Functions.Services;

internal sealed class QuestPdfGeneratorService : IPdfGeneratorService
{
    public byte[] Generate(InvoiceDocumentModel model) =>
        Document.Create(container =>
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Grey.Darken3));

                page.Header().Element(header => ComposeHeader(header, model));
                page.Content().Element(content => ComposeContent(content, model));
                page.Footer().Element(ComposeFooter);
            })).GeneratePdf();

    private static void ComposeHeader(IContainer container, InvoiceDocumentModel model)
    {
        string title = model.InvoiceType switch
        {
            InvoiceType.CreditNote => "CREDIT NOTE",
            _ => "INVOICE"
        };

        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("BOOKIFY").FontSize(24).Bold().FontColor(Colors.Blue.Darken3);
                column.Item().Text(title).FontSize(16).Bold().FontColor(Colors.Grey.Medium);
            });

            row.RelativeItem().Column(column =>
            {
                column.Item().AlignRight().Text($"Number: {model.InvoiceNumber}").Bold();
                column.Item().AlignRight().Text($"Date: {model.IssueDate:yyyy-MM-dd}");
                column.Item().AlignRight().Text($"Booking: {model.BookingId}");
            });
        });
    }

    private static void ComposeContent(IContainer container, InvoiceDocumentModel model) =>
        container.PaddingVertical(1, Unit.Centimetre).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(guestCol =>
                {
                    guestCol.Item().Text("Billed To:").Bold();
                    guestCol.Item().Text($"{model.GuestFirstName} {model.GuestLastName}");
                    guestCol.Item().Text(model.GuestEmail);
                });

                row.RelativeItem().Column(aptCol =>
                {
                    aptCol.Item().Text("Property:").Bold();
                    aptCol.Item().Text(model.ApartmentName);
                    aptCol.Item().Text(model.ApartmentAddress);
                });
            });

            column.Item().PaddingTop(1, Unit.Centimetre).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn();
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).Text("Description").Bold();
                    header.Cell().Element(HeaderCellStyle).Text("Period").Bold();
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("Nights").Bold();
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("Amount").Bold();

                    static IContainer HeaderCellStyle(IContainer container) =>
                        container.DefaultTextStyle(x => x.Bold())
                            .PaddingVertical(5)
                            .BorderBottom(1)
                            .BorderColor(Colors.Grey.Lighten1);
                });

                table.Cell().Element(BodyCellStyle).Text($"Accommodation - {model.ApartmentName}");
                table.Cell().Element(BodyCellStyle)
                    .Text($"{model.DurationStart:yyyy-MM-dd} to {model.DurationEnd:yyyy-MM-dd}");
                table.Cell().Element(BodyCellStyle).AlignRight()
                    .Text(model.TotalNights.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCellStyle).AlignRight().Text($"{model.TotalAmount:F2} {model.Currency}");

                static IContainer BodyCellStyle(IContainer container) =>
                    container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
            });

            column.Item().PaddingTop(0.5f, Unit.Centimetre).AlignRight().Column(summary =>
            {
                summary.Item().Text($"Tax ({model.Currency}): {model.TaxAmount:F2}");
                summary.Item().Text($"Total ({model.Currency}): {model.TotalAmount:F2}").FontSize(14).Bold()
                    .FontColor(Colors.Blue.Darken3);
            });
        });

    private static void ComposeFooter(IContainer container) =>
        container.AlignBottom().AlignCenter().Text(x =>
        {
            x.Span("Thank you for choosing Bookify. Page ");
            x.CurrentPageNumber();
            x.Span(" of ");
            x.TotalPages();
        });
}
