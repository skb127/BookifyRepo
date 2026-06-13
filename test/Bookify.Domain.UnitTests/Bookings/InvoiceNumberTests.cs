using Bookify.Domain.Bookings;
using FluentAssertions;

namespace Bookify.Domain.UnitTests.Bookings;

public class InvoiceNumberTests
{
    [Fact]
    public void Create_ShouldGenerateInvoiceFormat()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var expectedValue = $"INV-{bookingId:N}".ToUpperInvariant();

        // Act
        var invoiceNumber = InvoiceNumber.Create(bookingId);

        // Assert
        invoiceNumber.Value.Should().Be(expectedValue);
        invoiceNumber.ToString().Should().Be(expectedValue);
    }

    [Fact]
    public void CreateCreditNote_ShouldGenerateCreditNoteFormat()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var expectedValue = $"CN-{bookingId:N}".ToUpperInvariant();

        // Act
        var invoiceNumber = InvoiceNumber.CreateCreditNote(bookingId);

        // Assert
        invoiceNumber.Value.Should().Be(expectedValue);
        invoiceNumber.ToString().Should().Be(expectedValue);
    }

    [Fact]
    public void InvoiceNumbers_ShouldBeEqual_WhenCreatedWithSameBookingIdAndSameType()
    {
        // Arrange
        var bookingId = Guid.NewGuid();

        // Act
        var first = InvoiceNumber.Create(bookingId);
        var second = InvoiceNumber.Create(bookingId);

        // Assert
        first.Should().Be(second);
    }

    [Fact]
    public void InvoiceAndCreditNote_ShouldBeDifferent_WhenCreatedWithSameBookingId()
    {
        // Arrange
        var bookingId = Guid.NewGuid();

        // Act
        var invoice = InvoiceNumber.Create(bookingId);
        var creditNote = InvoiceNumber.CreateCreditNote(bookingId);

        // Assert
        invoice.Should().NotBe(creditNote);
        invoice.Value.Should().NotBe(creditNote.Value);
    }
}
