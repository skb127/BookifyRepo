using System.Linq.Expressions;
using Bookify.Application.Abstractions.Clock;
using Bookify.Application.Abstractions.Messaging;
using Bookify.Application.Bookings.Events;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using Bookify.Domain.Bookings;
using Bookify.Domain.Bookings.Events;
using Bookify.Domain.CancellationPolicies;
using Bookify.Domain.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;

namespace Bookify.Application.UnitTests.Booking.Events;

public class BookingRefundCompletedInvoiceHandlerTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    private readonly IBookingRepository _bookingRepositoryMock;
    private readonly IInvoiceRepository _invoiceRepositoryMock;
    private readonly IUnitOfWork _unitOfWorkMock;
    private readonly IDateTimeProvider _dateTimeProviderMock;
    private readonly IMessagePublisher _messagePublisherMock;
    private readonly IOptions<ServiceBusQueuesOptions> _queuesOptionsMock;
    private readonly BookingRefundCompletedInvoiceHandler _handler;

    public BookingRefundCompletedInvoiceHandlerTests()
    {
        _bookingRepositoryMock = Substitute.For<IBookingRepository>();
        _invoiceRepositoryMock = Substitute.For<IInvoiceRepository>();
        _unitOfWorkMock = Substitute.For<IUnitOfWork>();
        _dateTimeProviderMock = Substitute.For<IDateTimeProvider>();
        _messagePublisherMock = Substitute.For<IMessagePublisher>();
        _queuesOptionsMock = Microsoft.Extensions.Options.Options.Create(new ServiceBusQueuesOptions
            { InvoiceRequests = "invoice-requests" });

        _dateTimeProviderMock.UtcNow.Returns(UtcNow);

        _handler = new BookingRefundCompletedInvoiceHandler(
            _bookingRepositoryMock,
            _invoiceRepositoryMock,
            _unitOfWorkMock,
            _dateTimeProviderMock,
            _messagePublisherMock,
            _queuesOptionsMock);
    }

    private static Domain.Bookings.Booking CreateCancelledBookingWithRefund(
        decimal refundAmount = 150.00m,
        string refundReason = "Cancelled by Guest")
    {
        var apartment = new Apartment(
            Guid.CreateVersion7(),
            Guid.NewGuid(),
            new Name("Test Apartment"),
            new Description("Test Description"),
            new Address("Spain", "Madrid", "28001", "Madrid", "Calle Mayor"),
            new Money(10.0m, Currency.Eur),
            Money.Zero(Currency.Eur),
            [],
            UtcNow,
            false,
            null,
            1,
            3,
            1,
            2,
            Money.Zero(Currency.Eur));

        var booking = Domain.Bookings.Booking.Reserve(
            apartment,
            Guid.NewGuid(),
            DateRange.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 16)),
            UtcNow,
            new PricingService());

        booking.MarkAsPaid("intent_test", UtcNow);

        var policy = CancellationPolicy.Create("Standard", 0m, 0m, 0m, 0m, 24, true, UtcNow);
        var engine = new CancellationPolicyEngine();
        booking.Cancel(UtcNow, policy, engine, false);
        booking.InitiateRefund(refundAmount, "EUR", refundReason, UtcNow);
        booking.CompleteRefund(UtcNow);

        return booking;
    }

    private static Invoice CreateOriginalInvoice(Guid bookingId, decimal totalAmount = 150.00m,
        decimal taxAmount = 15.00m) =>
        Invoice.CreateForBooking(bookingId, totalAmount, taxAmount, "EUR", UtcNow);

    [Fact]
    public async Task
        Handle_ShouldCreateCreditNoteAndPublishMessage_WhenBookingAndOriginalInvoiceExistAndNoCreditNoteExists()
    {
        // Arrange
        Domain.Bookings.Booking booking = CreateCancelledBookingWithRefund();
        Guid bookingId = booking.Id;
        var domainEvent = new BookingRefundCompletedDomainEvent(bookingId);
        Invoice originalInvoice = CreateOriginalInvoice(bookingId);

        _invoiceRepositoryMock.GetAsync(Arg.Any<Expression<Func<Invoice, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                Func<Invoice, bool> predicate = callInfo.Arg<Expression<Func<Invoice, bool>>>().Compile();
                Invoice dummyCreditNote =
                    Invoice.CreateCreditNote(bookingId, originalInvoice.Id, 150.00m, 15.00m, "EUR", UtcNow);

                if (predicate(dummyCreditNote))
                {
                    return null;
                }

                return predicate(originalInvoice) ? originalInvoice : null;
            });

        _bookingRepositoryMock.GetWithRefundAsync(bookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        _invoiceRepositoryMock.Received(1).Add(Arg.Is<Invoice>(i =>
            i.BookingId == bookingId &&
            i.OriginalInvoiceId == originalInvoice.Id &&
            i.TotalAmount == 150.00m &&
            i.Currency == "EUR" &&
            i.InvoiceType == InvoiceType.CreditNote &&
            i.Status == InvoiceStatus.Pending));

        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        await _messagePublisherMock.Received(1).PublishAsync(
            "invoice-requests",
            Arg.Is<object>(m =>
                m.GetType().GetProperty("BookingId")!.GetValue(m)!.Equals(bookingId) &&
                m.GetType().GetProperty("InvoiceType")!.GetValue(m)!.ToString() == "CreditNote"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReuseExistingCreditNoteAndPublishMessage_WhenCreditNoteAlreadyExists()
    {
        // Arrange
        Guid bookingId = Guid.CreateVersion7();
        var domainEvent = new BookingRefundCompletedDomainEvent(bookingId);
        Invoice originalInvoice = CreateOriginalInvoice(bookingId);
        Invoice existingCreditNote =
            Invoice.CreateCreditNote(bookingId, originalInvoice.Id, 150.00m, 15.00m, "EUR", UtcNow);

        _invoiceRepositoryMock.GetAsync(Arg.Any<Expression<Func<Invoice, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(existingCreditNote);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        _invoiceRepositoryMock.DidNotReceive().Add(Arg.Any<Invoice>());
        await _unitOfWorkMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _bookingRepositoryMock.DidNotReceive().GetWithRefundAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        await _messagePublisherMock.Received(1).PublishAsync(
            "invoice-requests",
            Arg.Is<object>(m =>
                m.GetType().GetProperty("InvoiceId")!.GetValue(m)!.Equals(existingCreditNote.Id) &&
                m.GetType().GetProperty("BookingId")!.GetValue(m)!.Equals(bookingId) &&
                m.GetType().GetProperty("InvoiceType")!.GetValue(m)!.ToString() == "CreditNote"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotCreateCreditNote_WhenBookingNotFound()
    {
        // Arrange
        var domainEvent = new BookingRefundCompletedDomainEvent(Guid.NewGuid());

        _invoiceRepositoryMock.GetAsync(Arg.Any<Expression<Func<Invoice, bool>>>(), Arg.Any<CancellationToken>())
            .ReturnsNull();

        _bookingRepositoryMock.GetWithRefundAsync(domainEvent.BookingId, Arg.Any<CancellationToken>())
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
    public async Task Handle_ShouldNotCreateCreditNote_WhenOriginalInvoiceNotFound()
    {
        // Arrange
        Domain.Bookings.Booking booking = CreateCancelledBookingWithRefund();
        Guid bookingId = booking.Id;
        var domainEvent = new BookingRefundCompletedDomainEvent(bookingId);

        _invoiceRepositoryMock.GetAsync(Arg.Any<Expression<Func<Invoice, bool>>>(), Arg.Any<CancellationToken>())
            .ReturnsNull();

        _bookingRepositoryMock.GetWithRefundAsync(bookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

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
        Domain.Bookings.Booking booking = CreateCancelledBookingWithRefund();
        Guid bookingId = booking.Id;
        var domainEvent = new BookingRefundCompletedDomainEvent(bookingId);
        Invoice originalInvoice = CreateOriginalInvoice(bookingId);

        _invoiceRepositoryMock.GetAsync(Arg.Any<Expression<Func<Invoice, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                Func<Invoice, bool> predicate = callInfo.Arg<Expression<Func<Invoice, bool>>>().Compile();
                Invoice dummyCreditNote =
                    Invoice.CreateCreditNote(bookingId, originalInvoice.Id, 150.00m, 15.00m, "EUR", UtcNow);

                if (predicate(dummyCreditNote))
                {
                    return null;
                }

                return predicate(originalInvoice) ? originalInvoice : null;
            });

        _bookingRepositoryMock.GetWithRefundAsync(bookingId, Arg.Any<CancellationToken>())
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
        Domain.Bookings.Booking booking = CreateCancelledBookingWithRefund();
        Guid bookingId = booking.Id;
        var domainEvent = new BookingRefundCompletedDomainEvent(bookingId);
        Invoice originalInvoice = CreateOriginalInvoice(bookingId);

        _invoiceRepositoryMock.GetAsync(Arg.Any<Expression<Func<Invoice, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                Func<Invoice, bool> predicate = callInfo.Arg<Expression<Func<Invoice, bool>>>().Compile();
                Invoice dummyCreditNote =
                    Invoice.CreateCreditNote(bookingId, originalInvoice.Id, 150.00m, 15.00m, "EUR", UtcNow);

                if (predicate(dummyCreditNote))
                {
                    return null;
                }

                return predicate(originalInvoice) ? originalInvoice : null;
            });

        _bookingRepositoryMock.GetWithRefundAsync(bookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        _messagePublisherMock.PublishAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Service Bus error"));

        // Act
        Func<Task> act = async () => await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _unitOfWorkMock.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCreateCreditNote_WithPartialRefundAmount()
    {
        // Arrange: Booking total 150 EUR, refund amount 120 EUR
        Domain.Bookings.Booking booking =
            CreateCancelledBookingWithRefund(refundAmount: 120.00m, refundReason: "Cancelled by Guest");
        Guid bookingId = booking.Id;
        var domainEvent = new BookingRefundCompletedDomainEvent(bookingId);
        Invoice originalInvoice = CreateOriginalInvoice(bookingId);

        _invoiceRepositoryMock.GetAsync(Arg.Any<Expression<Func<Invoice, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                Func<Invoice, bool> predicate = callInfo.Arg<Expression<Func<Invoice, bool>>>().Compile();
                return predicate(originalInvoice) ? originalInvoice : null;
            });

        _bookingRepositoryMock.GetWithRefundAsync(bookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        _invoiceRepositoryMock.Received(1).Add(Arg.Is<Invoice>(i =>
            i.BookingId == bookingId &&
            i.OriginalInvoiceId == originalInvoice.Id &&
            i.TotalAmount == 120.00m &&
            i.InvoiceType == InvoiceType.CreditNote));
    }

    [Fact]
    public async Task Handle_ShouldCalculateProportionalTax_ForPartialRefund()
    {
        // Arrange: Booking total 150 EUR, original tax 15 EUR, refund amount 120 EUR (80%)
        // Expected proportional tax: 15 * (120/150) = 12.00 EUR
        Domain.Bookings.Booking booking = CreateCancelledBookingWithRefund(refundAmount: 120.00m);
        Guid bookingId = booking.Id;
        var domainEvent = new BookingRefundCompletedDomainEvent(bookingId);
        Invoice originalInvoice = CreateOriginalInvoice(bookingId);

        _invoiceRepositoryMock.GetAsync(Arg.Any<Expression<Func<Invoice, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                Func<Invoice, bool> predicate = callInfo.Arg<Expression<Func<Invoice, bool>>>().Compile();
                return predicate(originalInvoice) ? originalInvoice : null;
            });

        _bookingRepositoryMock.GetWithRefundAsync(bookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        _invoiceRepositoryMock.Received(1).Add(Arg.Is<Invoice>(i =>
            i.BookingId == bookingId &&
            i.TaxAmount == 12.00m &&
            i.TotalAmount == 120.00m));
    }
}
