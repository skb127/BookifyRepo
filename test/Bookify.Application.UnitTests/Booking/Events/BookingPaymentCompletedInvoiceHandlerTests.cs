using System.Linq.Expressions;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Bookings.Events;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using Bookify.Domain.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;

namespace Bookify.Application.UnitTests.Booking.Events;

public class BookingPaymentCompletedInvoiceHandlerTests
{
    private static readonly DateTime UtcNow = DateTime.UtcNow;

    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IInvoiceRepository _invoiceRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IMessagePublisher _messagePublisherMock;
    private readonly IOptions<ServiceBusQueuesOptions> _queuesOptionsMock;
    private readonly BookingPaymentCompletedInvoiceHandler _handler;

    public BookingPaymentCompletedInvoiceHandlerTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _invoiceRepositoryMock = Substitute.For<IInvoiceRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _messagePublisherMock = Substitute.For<IMessagePublisher>();
        _queuesOptionsMock = Microsoft.Extensions.Options.Options.Create(new ServiceBusQueuesOptions { InvoiceRequests = "invoice-requests" });

        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _handler = new BookingPaymentCompletedInvoiceHandler(
            _bookingRepositoryMock,
            _invoiceRepositoryMock,
            _unitOfWorkMock,
            _dateTimeProviderMock,
            _messagePublisherMock,
            _queuesOptionsMock);
    }

    private static Domain.Bookings.Booking CreateTestBooking(Guid bookingId)
    {
        var booking = (Domain.Bookings.Booking)Activator.CreateInstance(typeof(Domain.Bookings.Booking), true)!;
        typeof(Domain.Bookings.Booking).GetProperty("Id")!.SetValue(booking, bookingId);
        typeof(Domain.Bookings.Booking).GetProperty("TotalPrice")!.SetValue(booking, new Money(150.00m, Currency.Eur));
        return booking;
    }

    [Fact]
    public async Task Handle_ShouldCreateInvoiceAndPublishMessage_WhenBookingExistsAndNoInvoiceExists()
    {
        // Arrange
        Guid bookingId = Guid.CreateVersion7();
        var domainEvent = new BookingPaymentCompletedDomainEvent(bookingId, "pi_test_123");
        Domain.Bookings.Booking booking = CreateTestBooking(bookingId);

        _invoiceRepositoryMock.GetAsync(Arg.Any<Expression<Func<Invoice, bool>>>(), Arg.Any<CancellationToken>())
            .ReturnsNull();

        _bookingRepositoryMock.GetWithTaxesAsync(bookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        _invoiceRepositoryMock.Received(1).Add(Arg.Is<Invoice>(i =>
            i.BookingId == bookingId &&
            i.TotalAmount == 150.00m &&
            i.Currency == "EUR" &&
            i.InvoiceType == InvoiceType.Invoice &&
            i.Status == InvoiceStatus.Pending));

        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        await _messagePublisherMock.Received(1).PublishAsync(
            "invoice-requests",
            Arg.Is<object>(m =>
                m.GetType().GetProperty("BookingId")!.GetValue(m)!.Equals(bookingId) &&
                m.GetType().GetProperty("InvoiceType")!.GetValue(m)!.ToString() == "Invoice"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReuseExistingInvoiceAndPublishMessage_WhenInvoiceAlreadyExists()
    {
        // Arrange
        Guid bookingId = Guid.CreateVersion7();
        var domainEvent = new BookingPaymentCompletedDomainEvent(bookingId, "pi_test_123");
        Invoice existingInvoice = Invoice.CreateForBooking(bookingId, 150.00m, 0m, "EUR", UtcNow);

        _invoiceRepositoryMock.GetAsync(Arg.Any<Expression<Func<Invoice, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(existingInvoice);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        _invoiceRepositoryMock.DidNotReceive().Add(Arg.Any<Invoice>());
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _bookingRepositoryMock.DidNotReceive().GetWithTaxesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        await _messagePublisherMock.Received(1).PublishAsync(
            "invoice-requests",
            Arg.Is<object>(m =>
                m.GetType().GetProperty("InvoiceId")!.GetValue(m)!.Equals(existingInvoice.Id) &&
                m.GetType().GetProperty("BookingId")!.GetValue(m)!.Equals(bookingId) &&
                m.GetType().GetProperty("InvoiceType")!.GetValue(m)!.ToString() == "Invoice"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotCreateInvoice_WhenBookingNotFound()
    {
        // Arrange
        var domainEvent = new BookingPaymentCompletedDomainEvent(Guid.NewGuid(), "pi_test_123");

        _invoiceRepositoryMock.GetAsync(Arg.Any<Expression<Func<Invoice, bool>>>(), Arg.Any<CancellationToken>())
            .ReturnsNull();

        _bookingRepositoryMock.GetWithTaxesAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        _invoiceRepositoryMock.DidNotReceive().Add(Arg.Any<Invoice>());
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _messagePublisherMock.DidNotReceiveWithAnyArgs()
            .PublishAsync<object>(default!, default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldNotPublish_WhenSaveChangesFails()
    {
        // Arrange
        Guid bookingId = Guid.CreateVersion7();
        var domainEvent = new BookingPaymentCompletedDomainEvent(bookingId, "pi_test_123");
        Domain.Bookings.Booking booking = CreateTestBooking(bookingId);

        _invoiceRepositoryMock.GetAsync(Arg.Any<Expression<Func<Invoice, bool>>>(), Arg.Any<CancellationToken>())
            .ReturnsNull();

        _bookingRepositoryMock.GetWithTaxesAsync(bookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _unitOfWorkMock.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        Func<Task> act = async () => await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _messagePublisherMock.DidNotReceiveWithAnyArgs()
            .PublishAsync<object>(default!, default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldPropagateException_WhenPublishFails()
    {
        // Arrange
        Guid bookingId = Guid.CreateVersion7();
        var domainEvent = new BookingPaymentCompletedDomainEvent(bookingId, "pi_test_123");
        Domain.Bookings.Booking booking = CreateTestBooking(bookingId);

        _invoiceRepositoryMock.GetAsync(Arg.Any<Expression<Func<Invoice, bool>>>(), Arg.Any<CancellationToken>())
            .ReturnsNull();

        _bookingRepositoryMock.GetWithTaxesAsync(bookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _messagePublisherMock.PublishAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Service Bus error"));

        // Act
        Func<Task> act = async () => await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
