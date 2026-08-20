using System.Globalization;
using Bookify.Functions.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Bookify.Functions.Services;

internal sealed class QuestPdfGeneratorService : IPdfGeneratorService
{
    public byte[] Generate(InvoiceDocumentData data) =>
        Document.Create(container =>
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                page.Header().Element(header => ComposeHeader(header, data.Document));
                page.Content().Element(content => ComposeContent(content, data));
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
                column.Item().Text("BOOKIFY").FontSize(22).Bold().FontColor(Colors.Blue.Darken3);
                column.Item().Text(title).FontSize(14).Bold().FontColor(Colors.Grey.Medium);
            });

            row.RelativeItem().Column(column =>
            {
                column.Item().AlignRight().Text($"Number: {model.InvoiceNumber}").Bold();
                column.Item().AlignRight().Text($"Date: {model.IssueDate:yyyy-MM-dd}");
                column.Item().AlignRight().Text($"Booking: {model.BookingId}");
                if (model.InvoiceType == InvoiceType.CreditNote && !string.IsNullOrEmpty(model.OriginalInvoiceNumber))
                {
                    column.Item().AlignRight().Text($"Ref. Invoice: {model.OriginalInvoiceNumber}").FontColor(Colors.Grey.Darken1);
                }
            });
        });
    }

    private static void ComposeContent(IContainer container, InvoiceDocumentData data)
    {
        InvoiceDocumentModel model = data.Document;

        container.PaddingVertical(0.8f, Unit.Centimetre).Column(column =>
        {
            // Billed To & Property Info
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

            if (model.InvoiceType == InvoiceType.CreditNote)
            {
                column.Item().Element(c => ComposeCreditNoteContent(c, data));
            }
            else
            {
                column.Item().Element(c => ComposeInvoiceContent(c, data));
            }
        });
    }

    private static void ComposeInvoiceContent(IContainer container, InvoiceDocumentData data)
    {
        InvoiceDocumentModel model = data.Document;
        decimal pricePerNight = model.TotalNights > 0
            ? Math.Round(model.PriceForPeriod / model.TotalNights, 2)
            : model.PriceForPeriod;

        decimal subtotal = model.PriceForPeriod + model.CleaningFee + model.AmenitiesUpCharge + model.ExtraGuestCharge;

        container.PaddingTop(0.8f, Unit.Centimetre).Column(column =>
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3.5f);
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

                    static IContainer HeaderCellStyle(IContainer c) =>
                        c.DefaultTextStyle(x => x.Bold())
                            .PaddingVertical(5)
                            .BorderBottom(1)
                            .BorderColor(Colors.Grey.Lighten1);
                });

                // Accommodation
                table.Cell().Element(BodyCellStyle).Column(col =>
                {
                    col.Item().Text($"Accommodation - {model.ApartmentName}");
                    col.Item().Text($"({CurrencyFormatter.Format(pricePerNight, model.Currency)} / night)").FontSize(9).FontColor(Colors.Grey.Darken1);
                });
                table.Cell().Element(BodyCellStyle)
                    .Text($"{model.DurationStart:yyyy-MM-dd} to {model.DurationEnd:yyyy-MM-dd}");
                table.Cell().Element(BodyCellStyle).AlignRight()
                    .Text(model.TotalNights.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(BodyCellStyle).AlignRight()
                    .Text(CurrencyFormatter.Format(model.PriceForPeriod, model.Currency));

                // Cleaning fee
                if (model.CleaningFee > 0)
                {
                    table.Cell().Element(BodyCellStyle).Text("Cleaning Fee");
                    table.Cell().Element(BodyCellStyle).Text("-");
                    table.Cell().Element(BodyCellStyle).AlignRight().Text("-");
                    table.Cell().Element(BodyCellStyle).AlignRight().Text(CurrencyFormatter.Format(model.CleaningFee, model.Currency));
                }

                // Amenities upcharge
                if (model.AmenitiesUpCharge > 0)
                {
                    table.Cell().Element(BodyCellStyle).Text("Amenities Upcharge");
                    table.Cell().Element(BodyCellStyle).Text("-");
                    table.Cell().Element(BodyCellStyle).AlignRight().Text("-");
                    table.Cell().Element(BodyCellStyle).AlignRight().Text(CurrencyFormatter.Format(model.AmenitiesUpCharge, model.Currency));
                }

                // Extra guest charge
                if (model.ExtraGuestCharge > 0)
                {
                    table.Cell().Element(BodyCellStyle).Text("Extra Guest Charge");
                    table.Cell().Element(BodyCellStyle).Text("-");
                    table.Cell().Element(BodyCellStyle).AlignRight().Text("-");
                    table.Cell().Element(BodyCellStyle).AlignRight().Text(CurrencyFormatter.Format(model.ExtraGuestCharge, model.Currency));
                }

                static IContainer BodyCellStyle(IContainer c) =>
                    c.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
            });

            // Summary section
            column.Item().PaddingTop(0.5f, Unit.Centimetre).AlignRight().Column(summary =>
            {
                summary.Item().Text($"Subtotal: {CurrencyFormatter.Format(subtotal, model.Currency)}");

                if (data.TaxLines.Count > 0)
                {
                    foreach (TaxLineModel tax in data.TaxLines)
                    {
                        string rateDisplay = tax.TaxType == 0 ? $" ({tax.Rate:P0})" : string.Empty;
                        summary.Item().Text($"{tax.TaxName}{rateDisplay}: {CurrencyFormatter.Format(tax.Amount, model.Currency)}");
                    }
                }
                else if (model.TaxAmount > 0)
                {
                    summary.Item().Text($"Taxes: {CurrencyFormatter.Format(model.TaxAmount, model.Currency)}");
                }

                summary.Item().PaddingTop(4).Text($"Total: {CurrencyFormatter.Format(model.TotalAmount, model.Currency)}")
                    .FontSize(13).Bold().FontColor(Colors.Blue.Darken3);
            });
        });
    }

    private static void ComposeCreditNoteContent(IContainer container, InvoiceDocumentData data)
    {
        InvoiceDocumentModel model = data.Document;
        decimal originalTotal = model.OriginalTotalAmount ?? model.TotalAmount;
        decimal refundAmount = model.RefundAmount ?? model.TotalAmount;
        decimal penaltyAmount = originalTotal - refundAmount;
        decimal penaltyPercentage = originalTotal > 0 && penaltyAmount > 0
            ? Math.Round(penaltyAmount / originalTotal * 100, 0)
            : 0;

        container.PaddingTop(0.8f, Unit.Centimetre).Column(column =>
        {
            // Refund Details Card
            column.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(details =>
            {
                details.Item().Text("Refund Details").Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
                details.Item().PaddingTop(4).Text($"Reason: {model.RefundReason ?? "Booking Cancellation"}");
                details.Item().Text($"Stay Period: {model.DurationStart:yyyy-MM-dd} to {model.DurationEnd:yyyy-MM-dd} ({model.TotalNights} nights)");
            });

            // Original Charges Breakdown Card
            column.Item().PaddingTop(0.5f, Unit.Centimetre).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(orig =>
            {
                string origRef = !string.IsNullOrEmpty(model.OriginalInvoiceNumber) ? $" (Invoice {model.OriginalInvoiceNumber})" : string.Empty;
                orig.Item().Text($"Original Charges{origRef}").Bold().FontColor(Colors.Grey.Darken2);

                orig.Item().PaddingTop(4).Row(r =>
                {
                    r.RelativeItem().Text($"Accommodation ({model.TotalNights} nights)");
                    r.RelativeItem().AlignRight().Text(CurrencyFormatter.Format(model.PriceForPeriod, model.Currency));
                });

                if (model.CleaningFee > 0)
                {
                    orig.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Cleaning Fee");
                        r.RelativeItem().AlignRight().Text(CurrencyFormatter.Format(model.CleaningFee, model.Currency));
                    });
                }

                if (model.AmenitiesUpCharge > 0)
                {
                    orig.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Amenities Upcharge");
                        r.RelativeItem().AlignRight().Text(CurrencyFormatter.Format(model.AmenitiesUpCharge, model.Currency));
                    });
                }

                if (model.ExtraGuestCharge > 0)
                {
                    orig.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Extra Guest Charge");
                        r.RelativeItem().AlignRight().Text(CurrencyFormatter.Format(model.ExtraGuestCharge, model.Currency));
                    });
                }

                decimal origTax = model.OriginalTaxAmount ?? model.TaxAmount;
                if (origTax > 0)
                {
                    orig.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Taxes");
                        r.RelativeItem().AlignRight().Text(CurrencyFormatter.Format(origTax, model.Currency));
                    });
                }

                orig.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                orig.Item().PaddingTop(4).Row(r =>
                {
                    r.RelativeItem().Text("Original Total").Bold();
                    r.RelativeItem().AlignRight().Text(CurrencyFormatter.Format(originalTotal, model.Currency)).Bold();
                });
            });

            // Refund Settlement Summary
            column.Item().PaddingTop(0.5f, Unit.Centimetre).AlignRight().Column(summary =>
            {
                if (penaltyAmount > 0 && penaltyPercentage > 0)
                {
                    summary.Item().Text($"Cancellation fee ({penaltyPercentage:0}%): -{CurrencyFormatter.Format(penaltyAmount, model.Currency)}")
                        .FontColor(Colors.Red.Darken2);
                }
                else
                {
                    summary.Item().Text("Full refund applied (100%)").FontColor(Colors.Green.Darken2);
                }

                summary.Item().PaddingTop(4).Text($"Credit Amount: -{CurrencyFormatter.Format(model.TotalAmount, model.Currency)}")
                    .FontSize(13).Bold().FontColor(Colors.Blue.Darken3);
            });
        });
    }

    private static void ComposeFooter(IContainer container) =>
        container.AlignBottom().AlignCenter().Text(x =>
        {
            x.Span("Thank you for choosing Bookify. Page ");
            x.CurrentPageNumber();
            x.Span(" of ");
            x.TotalPages();
        });
}
