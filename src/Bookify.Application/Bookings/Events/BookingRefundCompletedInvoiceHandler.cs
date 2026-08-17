using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using MediatR;
using Microsoft.Extensions.Options;

namespace Bookify.Application.Bookings.Events;

internal sealed class BookingRefundCompletedInvoiceHandler : INotificationHandler<BookingRefundCompletedDomainEvent>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMessagePublisher _messagePublisher;
    private readonly IOptions<ServiceBusQueuesOptions> _queuesOptions;

    public BookingRefundCompletedInvoiceHandler(
        IBookingRepository bookingRepository,
        IInvoiceRepository invoiceRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IMessagePublisher messagePublisher,
        IOptions<ServiceBusQueuesOptions> queuesOptions)
    {
        _bookingRepository = bookingRepository;
        _invoiceRepository = invoiceRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _messagePublisher = messagePublisher;
        _queuesOptions = queuesOptions;
    }

    public async Task Handle(BookingRefundCompletedDomainEvent notification, CancellationToken cancellationToken)
    {
        Invoice? existingCreditNote = await _invoiceRepository.GetAsync(
            i => i.BookingId == notification.BookingId && i.InvoiceType == InvoiceType.CreditNote,
            cancellationToken);

        Invoice creditNote;
        if (existingCreditNote is not null)
        {
            creditNote = existingCreditNote;
        }
        else
        {
            Booking? booking = await _bookingRepository.GetWithTaxesAsync(notification.BookingId, cancellationToken);
            if (booking is null)
            {
                return;
            }

            Invoice? originalInvoice = await _invoiceRepository.GetAsync(
                i => i.BookingId == notification.BookingId && i.InvoiceType == InvoiceType.Invoice,
                cancellationToken);

            if (originalInvoice is null)
            {
                return;
            }

            creditNote = Invoice.CreateCreditNote(
                booking.Id,
                originalInvoice.Id,
                booking.TotalPrice.Amount,
                originalInvoice.TaxAmount,
                booking.TotalPrice.Currency.Code,
                _dateTimeProvider.UtcNow);

            _invoiceRepository.Add(creditNote);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var message = new
        {
            InvoiceId = creditNote.Id,
            creditNote.BookingId,
            InvoiceType = creditNote.InvoiceType.ToString()
        };

        await _messagePublisher.PublishAsync(_queuesOptions.Value.InvoiceRequests, message, cancellationToken);
    }
}
