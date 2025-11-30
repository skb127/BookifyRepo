using Bookify.Application.Apartments.SearchApartments;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Abstractions;
using FluentAssertions;

namespace Bookify.Application.IntegrationTests.Apartments;

public class SearchApartmentsTests : BaseIntegrationTest
{
    public SearchApartmentsTests(IntegrationTestWebAppFactory factory) 
        : base(factory)
    {
    }

    [Fact]
    public async Task SearchApartments_ShouldReturnEmptyList_WhenDateRangeIsInvalid()
    {
        // Arrange
        var query = new SearchApartmentsQuery(new DateOnly(2026, 2, 20), new DateOnly(2026, 2, 12));

        // Act
        Result<IReadOnlyList<ApartmentResponse>> result = await Sender.Send(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchApartments_ShouldReturnEmptyList_WhenDateRangeIsValid()
    {
        // Arrange
        var query = new SearchApartmentsQuery(new DateOnly(2026, 2, 12), new DateOnly(2026, 2, 20));

        // Act
        Result<IReadOnlyList<ApartmentResponse>> result = await Sender.Send(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }
}
