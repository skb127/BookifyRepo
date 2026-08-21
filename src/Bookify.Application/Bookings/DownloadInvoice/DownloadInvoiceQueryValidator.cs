using FluentValidation;

namespace Bookify.Application.Bookings.DownloadInvoice;

internal sealed class DownloadInvoiceQueryValidator : AbstractValidator<DownloadInvoiceQuery>
{
    public DownloadInvoiceQueryValidator()
    {
        RuleFor(c => c.BookingId).NotEmpty();
        RuleFor(c => c.InvoiceId).NotEmpty();
    }
}
