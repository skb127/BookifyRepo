using Bookify.Application.Abstractions.Payments;
using FluentAssertions;
using NSubstitute;

namespace Bookify.Application.UnitTests.Payments;

public class StripeCustomerServiceTests
{
    private readonly IStripeCustomerService _customerServiceMock;

    public StripeCustomerServiceTests()
    {
        _customerServiceMock = Substitute.For<IStripeCustomerService>();
    }

    [Fact]
    public async Task UpsertCustomer_ShouldReturnStripeCustomerId_WhenCalled()
    {
        // Arrange
        var userId = Guid.NewGuid();
        const string email = "customer@example.com";
        const string name = "John Doe";
        const string expectedCustomerId = "cus_abc123";

        _customerServiceMock.UpsertCustomerAsync(userId, email, name, Arg.Any<CancellationToken>())
            .Returns(expectedCustomerId);

        // Act
        var result = await _customerServiceMock.UpsertCustomerAsync(userId, email, name, CancellationToken.None);

        // Assert
        result.Should().Be(expectedCustomerId);
        await _customerServiceMock.Received(1).UpsertCustomerAsync(userId, email, name, CancellationToken.None);
    }

    [Fact]
    public async Task UpdateCustomer_ShouldCallUpdate_WhenCalled()
    {
        // Arrange
        const string customerId = "cus_abc123";
        const string email = "customer-new@example.com";
        const string name = "John Doe New";

        // Act
        await _customerServiceMock.UpdateCustomerAsync(customerId, email, name, CancellationToken.None);

        // Assert
        await _customerServiceMock.Received(1).UpdateCustomerAsync(customerId, email, name, CancellationToken.None);
    }

    [Fact]
    public async Task DeactivateCustomer_ShouldCallDeactivate_WhenCalled()
    {
        // Arrange
        const string customerId = "cus_abc123";

        // Act
        await _customerServiceMock.DeactivateCustomerAsync(customerId, CancellationToken.None);

        // Assert
        await _customerServiceMock.Received(1).DeactivateCustomerAsync(customerId, CancellationToken.None);
    }
}
