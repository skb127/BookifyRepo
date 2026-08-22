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
            row.AutoItem().Column(column =>
            {
                column.Item().Text("BOOKIFY").FontSize(22).Bold().FontColor(Colors.Blue.Darken3);
                column.Item().Text(title).FontSize(14).Bold().FontColor(Colors.Grey.Medium);
            });

            row.RelativeItem().AlignRight().MaxWidth(330).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(65);
                    columns.RelativeColumn();
                });

                table.Cell().PaddingRight(8).Text("Number:").Bold();
                table.Cell().AlignRight().Text(model.InvoiceNumber).Bold().FontSize(9);

                table.Cell().PaddingRight(8).Text("Date:").FontColor(Colors.Grey.Darken2);
                table.Cell().AlignRight().Text($"{model.IssueDate:yyyy-MM-dd}");

                table.Cell().PaddingRight(8).Text("Booking:").FontColor(Colors.Grey.Darken2);
                table.Cell().AlignRight().Text(model.BookingId.ToString()).FontSize(8.5f);

                if (model.InvoiceType == InvoiceType.CreditNote && !string.IsNullOrEmpty(model.OriginalInvoiceNumber))
                {
                    table.Cell().PaddingRight(8).Text("Ref. Invoice:").FontColor(Colors.Grey.Darken1);
                    table.Cell().AlignRight().Text(model.OriginalInvoiceNumber).FontColor(Colors.Grey.Darken1).FontSize(9);
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
                    col.Item().Text($"({CurrencyFormatter.Format(pricePerNight, model.Currency)} / night)").FontSize(9)
                        .FontColor(Colors.Grey.Darken1);
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
                    table.Cell().Element(BodyCellStyle).AlignRight()
                        .Text(CurrencyFormatter.Format(model.CleaningFee, model.Currency));
                }

                // Amenities upcharge
                if (model.AmenitiesUpCharge > 0)
                {
                    table.Cell().Element(BodyCellStyle).Text("Amenities Upcharge");
                    table.Cell().Element(BodyCellStyle).Text("-");
                    table.Cell().Element(BodyCellStyle).AlignRight().Text("-");
                    table.Cell().Element(BodyCellStyle).AlignRight()
                        .Text(CurrencyFormatter.Format(model.AmenitiesUpCharge, model.Currency));
                }

                // Extra guest charge
                if (model.ExtraGuestCharge > 0)
                {
                    int extraGuests = Math.Max(0, model.GuestCount - model.BaseGuests);
                    table.Cell().Element(BodyCellStyle).Column(col =>
                    {
                        col.Item().Text("Extra Guest Charge");
                        col.Item().Text(
                                $"({CurrencyFormatter.Format(model.ExtraGuestFee, model.Currency)} x {extraGuests} guests x {model.TotalNights} nights)")
                            .FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                    table.Cell().Element(BodyCellStyle).Text("-");
                    table.Cell().Element(BodyCellStyle).AlignRight().Text("-");
                    table.Cell().Element(BodyCellStyle).AlignRight()
                        .Text(CurrencyFormatter.Format(model.ExtraGuestCharge, model.Currency));
                }

                static IContainer BodyCellStyle(IContainer c) =>
                    c.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);

                static IContainer SummaryLabelStyle(IContainer c) =>
                    c.PaddingVertical(5).PaddingRight(10).AlignRight();

                static IContainer SummaryValueStyle(IContainer c) =>
                    c.PaddingVertical(5).AlignRight();

                // Add a thicker line before the summary
                table.Cell().ColumnSpan(4).PaddingTop(5).BorderBottom(1).BorderColor(Colors.Grey.Darken2);

                table.Cell().ColumnSpan(3).Element(SummaryLabelStyle).Text("Subtotal:");
                table.Cell().Element(SummaryValueStyle).Text(CurrencyFormatter.Format(subtotal, model.Currency));

                if (data.TaxLines.Count > 0)
                {
                    foreach (TaxLineModel tax in data.TaxLines)
                    {
                        string rateDisplay = tax.TaxType == 0 ? $" ({tax.Rate:P0})" : string.Empty;
                        table.Cell().ColumnSpan(3).Element(SummaryLabelStyle).Text($"{tax.TaxName}{rateDisplay}:");
                        table.Cell().Element(SummaryValueStyle)
                            .Text(CurrencyFormatter.Format(tax.Amount, model.Currency));
                    }
                }
                else if (model.TaxAmount > 0)
                {
                    table.Cell().ColumnSpan(3).Element(SummaryLabelStyle).Text("Taxes:");
                    table.Cell().Element(SummaryValueStyle)
                        .Text(CurrencyFormatter.Format(model.TaxAmount, model.Currency));
                }
                else
                {
                    table.Cell().ColumnSpan(3).Element(SummaryLabelStyle).Text("Taxes (0%):");
                    table.Cell().Element(SummaryValueStyle).Text(CurrencyFormatter.Format(0m, model.Currency));
                }

                table.Cell().ColumnSpan(3).Element(SummaryLabelStyle).Text("Total:").FontSize(13).Bold()
                    .FontColor(Colors.Blue.Darken3);
                table.Cell().Element(SummaryValueStyle)
                    .Text(CurrencyFormatter.Format(model.TotalAmount, model.Currency))
                    .FontSize(13).Bold().FontColor(Colors.Blue.Darken3);
            }));
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
                details.Item()
                    .Text(
                        $"Stay Period: {model.DurationStart:yyyy-MM-dd} to {model.DurationEnd:yyyy-MM-dd} ({model.TotalNights} nights)");
            });

            // Original Charges Breakdown Card
            column.Item().PaddingTop(0.5f, Unit.Centimetre).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10)
                .Column(orig =>
                {
                    string origRef = !string.IsNullOrEmpty(model.OriginalInvoiceNumber)
                        ? $" (Invoice {model.OriginalInvoiceNumber})"
                        : string.Empty;
                    orig.Item().Text($"Original Charges{origRef}").Bold().FontColor(Colors.Grey.Darken2);

                    orig.Item().PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text($"Accommodation ({model.TotalNights} nights)");
                        r.RelativeItem().AlignRight()
                            .Text(CurrencyFormatter.Format(model.PriceForPeriod, model.Currency));
                    });

                    if (model.CleaningFee > 0)
                    {
                        orig.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Cleaning Fee");
                            r.RelativeItem().AlignRight()
                                .Text(CurrencyFormatter.Format(model.CleaningFee, model.Currency));
                        });
                    }

                    if (model.AmenitiesUpCharge > 0)
                    {
                        orig.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Amenities Upcharge");
                            r.RelativeItem().AlignRight()
                                .Text(CurrencyFormatter.Format(model.AmenitiesUpCharge, model.Currency));
                        });
                    }

                    if (model.ExtraGuestCharge > 0)
                    {
                        orig.Item().Row(r =>
                        {
                            int extraGuests = Math.Max(0, model.GuestCount - model.BaseGuests);
                            r.RelativeItem().Column(col =>
                            {
                                col.Item().Text("Extra Guest Charge");
                                col.Item().Text(
                                        $"({CurrencyFormatter.Format(model.ExtraGuestFee, model.Currency)} x {extraGuests} guests x {model.TotalNights} nights)")
                                    .FontSize(9).FontColor(Colors.Grey.Darken1);
                            });
                            r.RelativeItem().AlignRight()
                                .Text(CurrencyFormatter.Format(model.ExtraGuestCharge, model.Currency));
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
                    else
                    {
                        orig.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Taxes (0%)");
                            r.RelativeItem().AlignRight().Text(CurrencyFormatter.Format(0m, model.Currency));
                        });
                    }

                    orig.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    orig.Item().PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text("Original Total").Bold();
                        r.RelativeItem().AlignRight().Text(CurrencyFormatter.Format(originalTotal, model.Currency))
                            .Bold();
                    });
                });

            // Refund Settlement Summary
            column.Item().PaddingTop(0.5f, Unit.Centimetre).AlignRight().MaxWidth(300).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(85);
                });

                static IContainer SummaryLabelStyle(IContainer c) =>
                    c.PaddingVertical(5).PaddingRight(10).AlignRight();

                static IContainer SummaryValueStyle(IContainer c) =>
                    c.PaddingVertical(5).AlignRight();

                if (penaltyAmount > 0 && penaltyPercentage > 0)
                {
                    table.Cell().Element(SummaryLabelStyle).Text($"Cancellation fee ({penaltyPercentage:0}%):")
                        .FontColor(Colors.Red.Darken2);
                    table.Cell().Element(SummaryValueStyle)
                        .Text($"-{CurrencyFormatter.Format(penaltyAmount, model.Currency)}")
                        .FontColor(Colors.Red.Darken2);
                }
                else
                {
                    table.Cell().ColumnSpan(2).Element(SummaryLabelStyle).Text("Full refund applied (100%)")
                        .FontColor(Colors.Green.Darken2);
                }

                table.Cell().Element(SummaryLabelStyle).Text("Credit Amount:").FontSize(13).Bold()
                    .FontColor(Colors.Blue.Darken3);
                table.Cell().Element(SummaryValueStyle)
                    .Text($"-{CurrencyFormatter.Format(model.TotalAmount, model.Currency)}")
                    .FontSize(13).Bold().FontColor(Colors.Blue.Darken3);
            });
        });
    }

    private static void ComposeFooter(IContainer container) =>
        container.AlignBottom().Column(column =>
        {
            column.Item().AlignCenter().Text("Thank you for choosing Bookify.").FontSize(9)
                .FontColor(Colors.Grey.Darken1);
            column.Item().PaddingTop(8).AlignRight().Text(x =>
            {
                x.DefaultTextStyle(s => s.FontSize(9).FontColor(Colors.Grey.Darken1));
                x.Span("Page ");
                x.CurrentPageNumber();
                x.Span(" of ");
                x.TotalPages();
            });
        });
}
