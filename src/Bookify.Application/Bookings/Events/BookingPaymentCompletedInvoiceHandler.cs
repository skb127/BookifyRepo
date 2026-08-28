using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using MediatR;
using Microsoft.Extensions.Options;

namespace Bookify.Application.Bookings.Events;

internal sealed class BookingPaymentCompletedInvoiceHandler : 
    INotificationHandler<BookingPaymentCompletedDomainEvent>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMessagePublisher _messagePublisher;
    private readonly IOptions<ServiceBusQueuesOptions> _queuesOptions;

    public BookingPaymentCompletedInvoiceHandler(
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

    public async Task Handle(BookingPaymentCompletedDomainEvent notification, CancellationToken cancellationToken)
    {
        Invoice? existingInvoice = await _invoiceRepository.GetAsync(
            i => i.BookingId == notification.BookingId && i.InvoiceType == InvoiceType.Invoice,
            cancellationToken);

        Invoice invoice;
        if (existingInvoice is not null)
        {
            invoice = existingInvoice;
        }
        else
        {
            Booking? booking = await _bookingRepository.GetWithTaxesAsync(notification.BookingId, cancellationToken);
            if (booking is null)
            {
                return;
            }

            decimal taxAmount = booking.Taxes.Sum(t => t.CalculatedAmount.Amount);
            decimal totalAmount = booking.TotalPrice.Amount + taxAmount;

            invoice = Invoice.CreateForBooking(
                booking.Id,
                totalAmount,
                taxAmount,
                booking.TotalPrice.Currency.Code,
                _dateTimeProvider.UtcNow);

            _invoiceRepository.Add(invoice);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var message = new
        {
            InvoiceId = invoice.Id,
            invoice.BookingId,
            InvoiceType = invoice.InvoiceType.ToString()
        };

        await _messagePublisher.PublishAsync(_queuesOptions.Value.InvoiceRequests, message, cancellationToken);
    }
}
