using Bookify.Application.Bookings.DownloadInvoice;
using FluentValidation.TestHelper;

namespace Bookify.Application.UnitTests.Invoices;

public class DownloadInvoiceQueryValidatorTests
{
    private readonly DownloadInvoiceQueryValidator _validator = new();

    [Fact]
    public void BookingId_ShouldFail_WhenEmpty()
    {
        // Arrange
        var query = new DownloadInvoiceQuery(Guid.Empty, Guid.NewGuid());

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.BookingId);
    }

    [Fact]
    public void InvoiceId_ShouldFail_WhenEmpty()
    {
        // Arrange
        var query = new DownloadInvoiceQuery(Guid.NewGuid(), Guid.Empty);

        // Act & Assert
        _validator.TestValidate(query).ShouldHaveValidationErrorFor(q => q.InvoiceId);
    }

    [Fact]
    public void Query_ShouldSucceed_WhenAllFieldsAreValid()
    {
        // Arrange
        var query = new DownloadInvoiceQuery(Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }
}
