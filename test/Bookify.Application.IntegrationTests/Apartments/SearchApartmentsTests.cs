using Bookify.Application.Apartments.SearchApartments;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Domain.Abstractions;
using FluentAssertions;

namespace Bookify.Application.IntegrationTests.Apartments;

[Collection("IntegrationTests")]
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

    /// <summary>
    /// To test this properly, the database should be seeded with apartments that are available, uncomment app.SeedData(); in Program.cs
    /// before running the tests
    /// </summary>
    [Fact]
    public async Task SearchApartments_ShouldReturnApartments_WhenDateRangeIsValid()
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
