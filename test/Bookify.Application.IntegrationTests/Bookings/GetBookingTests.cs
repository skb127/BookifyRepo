using Bookify.Application.Bookings.GetBooking;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Bookings;
using FluentAssertions;

namespace Bookify.Application.IntegrationTests.Bookings;

public class GetBookingTests : BaseIntegrationTest
{
    private static readonly Guid BookingId = Guid.CreateVersion7();

    public GetBookingTests(IntegrationTestWebAppFactory factory) 
        : base(factory)
    {
    }

    [Fact]
    public async Task GetBookings_ShouldReturnFailure_WhenBookingIsNotFound()
    {
        // Arrange
        var command = new GetBookingQuery(BookingId);

        // Act
        Result<BookingResponse> result = await Sender.Send(command);

        // Assert
        result.Error.Should().Be(BookingErrors.NotFound);
    }
}
