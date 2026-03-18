using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Api.Controllers.Apartments;
using Bookify.Application.Apartments.SearchApartments;
using Bookify.Application.Common;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.IntegrationTests.Users;
using Bookify.Domain.Abstractions;
using Bookify.Domain.Apartments;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

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
        // Arrange - New signature uses nullable optional params.
        var query = new SearchApartmentsQuery(
            new DateOnly(2027, 2, 20), // start
            new DateOnly(2027, 2, 12), // end (invalid because > start, will fail validation, but validating the handler's bounds logic)
            null, null, null, null, null, null, 1, 20);

        // Act
        Func<Task> act = async () => await Sender.Send(query).ConfigureAwait(false);

        // Assert - It fails fast at the validation behaviour, throwing a ValidationException
        await act.Should().ThrowAsync<Exceptions.ValidationException>();
    }

    /// <summary>
    /// To test this properly, the database should be seeded with apartments that are available, uncomment app.SeedData(); in Program.cs
    /// before running the tests.
    /// </summary>
    [Fact]
    public async Task SearchApartments_ShouldReturnApartments_WhenDateRangeIsValid()
    {
        // Arrange - Dates in the far future to avoid conflicting with existing seed
        var query = new SearchApartmentsQuery(
            new DateOnly(2027, 2, 12),
            new DateOnly(2027, 2, 20),
            null, null, null, null, null, null, 1, 20);

        // Act
        Result<PagedResponse<ApartmentResponse>> result = await Sender.Send(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task SearchApartments_ShouldReturnPagedResults_WhenNoFiltersProvided()
    {
        // Arrange
        var query = new SearchApartmentsQuery(
            null, null, null, null, null, null, null, null, 1, 10);

        // Act
        Result<PagedResponse<ApartmentResponse>> result = await Sender.Send(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(10);
        result.Value.TotalCount.Should().BeGreaterThan(1);
        result.Value.Items.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task SearchApartments_ShouldReturnBadRequest_WhenOnlyStartDateProvided()
    {
        // Arrange
        var query = new SearchApartmentsQuery(
            new DateOnly(2027, 1, 1),
            null, // missing end date
            null, null, null, null, null, null, 1, 20);

        // Act
        Func<Task> act = async () => await Sender.Send(query).ConfigureAwait(false);

        // Assert
        await act.Should().ThrowAsync<Exceptions.ValidationException>();
    }

    [Fact]
    public async Task SearchApartments_ShouldReturnBadRequest_WhenMinPriceGreaterThanMaxPrice()
    {
        // Arrange
        var query = new SearchApartmentsQuery(
            null, null, null, null,
            200m, 100m, // min > max
            null, null, 1, 20);

        // Act
        Func<Task> act = async () => await Sender.Send(query).ConfigureAwait(false);

        // Assert
        await act.Should().ThrowAsync<Exceptions.ValidationException>();
    }

    [Fact]
    public async Task SearchApartments_ShouldReturnBadRequest_WhenCurrencyIsUnknown()
    {
        // Arrange
        var query = new SearchApartmentsQuery(
            null, null, null, null, null, null,
            "XYZ", // Invalid currency
            null, 1, 20);

        // Act
        Func<Task> act = async () => await Sender.Send(query).ConfigureAwait(false);

        // Assert
        await act.Should().ThrowAsync<Exceptions.ValidationException>();
    }

    [Fact]
    public async Task SearchApartments_ShouldReturnBadRequest_WhenPageSizeExceedsMaximum()
    {
        // Arrange
        var query = new SearchApartmentsQuery(
            null, null, null, null, null, null, null, null,
            1, 101); // max is 100

        // Act
        Func<Task> act = async () => await Sender.Send(query).ConfigureAwait(false);

        // Assert
        await act.Should().ThrowAsync<Exceptions.ValidationException>();
    }

    [Fact]
    public async Task SearchApartments_ShouldReturnUnauthorized_WhenNoToken()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/apartments", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SearchApartments_ShouldReturn200_WhenAuthenticatedWithNoFilters()
    {
        // Arrange - login as a standard user
        string accessToken = await GetAccessToken(
            UserData.CreateApartmentStandardUserRequest.Email,
            UserData.CreateApartmentStandardUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act - send request with query parameters for pagination
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/apartments?page=1&pageSize=20", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResponse = await response.Content.ReadFromJsonAsync<PagedResponse<ApartmentResponse>>();
        pagedResponse.Should().NotBeNull();
        pagedResponse.Page.Should().Be(1);
        pagedResponse.PageSize.Should().Be(20);
        pagedResponse.TotalCount.Should().BeGreaterThan(1);
        pagedResponse.Items.Should().NotBeNull();
        // Since we know the database is seeded with many apartments, page size of 5 should yield exactly 5 items
        pagedResponse.Items.Count.Should().Be(20);
    }

    [Fact]
    public async Task SearchApartments_ShouldReturn400_WhenStartDateWithoutEndDate()
    {
        // Arrange - login as a standard user
        string accessToken = await GetAccessToken(
            UserData.CreateApartmentStandardUserRequest.Email,
            UserData.CreateApartmentStandardUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        // Act - invalid query parameters
        HttpResponseMessage response = await HttpClient.GetAsync(new Uri("api/v1/apartments?startDate=2026-05-01", UriKind.Relative));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SearchApartments_ShouldReturnResults_WhenCityFilterMatches()
    {
        // Arrange
        await PromoteToAdminAsync(UserData.CreateApartmentAdminUserRequest.Email);
        string accessToken = await GetAccessToken(
            UserData.CreateApartmentAdminUserRequest.Email,
            UserData.CreateApartmentAdminUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            accessToken);

        var request = new CreateApartmentRequest(
            "City Test Apartment",
            "Description",
            new AddressRequest("Country", "State", "ZipCode", ApartmentData.TestCity_SearchFilter, "Street"),
            new MoneyRequest(100.0m, "USD"),
            new MoneyRequest(50.0m, "USD"),
            []);

        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", request);
        createResponse.IsSuccessStatusCode.Should().BeTrue();

        var query = new SearchApartmentsQuery(
            null, null,
            ApartmentData.TestCity_SearchFilter,
            null, null, null, null, null, 1, 10);

        // Act
        Result<PagedResponse<ApartmentResponse>> result = await Sender.Send(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.TotalCount.Should().BeGreaterThanOrEqualTo(1);
        result.Value.Items.Should().Contain(a => a.Address.City == ApartmentData.TestCity_SearchFilter);
    }

    [Fact]
    public async Task SearchApartments_ShouldReturnResults_WhenCurrencyFilterMatches()
    {
        // Arrange - We know the seed data generates apartments with "USD" currency
        var query = new SearchApartmentsQuery(
            null, null, null, null, null, null,
            "USD",
            null, 1, 10);

        // Act
        Result<PagedResponse<ApartmentResponse>> result = await Sender.Send(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Items.Should().NotBeEmpty();
        result.Value.Items.Should().OnlyContain(a => a.Price.Currency == "USD");
    }

    [Fact]
    public async Task SearchApartments_ShouldReturnEmpty_WhenPriceRangeHasNoMatches()
    {
        // Arrange - We know the seed data generates prices between 50 and 1000
        var query = new SearchApartmentsQuery(
            null, null, null, null,
            50000m, // min price
            100000m, // max price
            null, null, 1, 10);

        // Act
        Result<PagedResponse<ApartmentResponse>> result = await Sender.Send(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task SearchApartments_ShouldReturnResults_WhenPriceRangeCoversSeedData()
    {
        // Arrange - We know the seed data generates prices between 50 and 1000
        var query = new SearchApartmentsQuery(
            null, null, null, null,
            50m, // min price
            1000m, // max price
            null, null, 1, 10);

        // Act
        Result<PagedResponse<ApartmentResponse>> result = await Sender.Send(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Items.Should().NotBeEmpty();
        result.Value.Items.Should().OnlyContain(a => a.Price.Amount >= 50m && a.Price.Amount <= 1000m);
    }

    [Fact]
    public async Task SearchApartments_ShouldReturnResults_WhenAmenitiesFilterMatches()
    {
        // Arrange - We know the seed data generates apartments with Parking and MountainView amenities
        var query = new SearchApartmentsQuery(
            null, null, null, null, null, null, null,
            [(int)Amenity.Parking, (int)Amenity.MountainView],
            1, 10);

        // Act
        Result<PagedResponse<ApartmentResponse>> result = await Sender.Send(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Items.Should().NotBeEmpty();

        // Ensure all returned apartments have at least the requested amenities
        foreach (var apartment in result.Value.Items)
        {
            apartment.Amenities.Should().Contain((int)Amenity.Parking);
            apartment.Amenities.Should().Contain((int)Amenity.MountainView);
        }
    }

    [Fact]
    public async Task SearchApartments_ShouldReturnEmpty_WhenAmenitiesFilterHasNoMatches()
    {
        // Arrange - We know the seed data doesn't contain apartments with PetFriendly amenity
        var query = new SearchApartmentsQuery(
            null, null, null, null, null, null, null,
            [(int)Amenity.PetFriendly],
            1, 10);

        // Act
        Result<PagedResponse<ApartmentResponse>> result = await Sender.Send(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task SearchApartments_ShouldReturn_ZeroAverageRating_ForApartmentWithNoReviews()
    {
        // Arrange - use a specific city to isolate our new apartment in search results
        string uniqueCity = $"City_{Guid.CreateVersion7()}";

        await PromoteToAdminAsync(UserData.CreateApartmentAdminUserRequest.Email);
        string adminToken = await GetAccessToken(
            UserData.CreateApartmentAdminUserRequest.Email,
            UserData.CreateApartmentAdminUserRequest.Password);

        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            adminToken);

        var request = new CreateApartmentRequest(
            "No Reviews Apartment",
            "Description",
            new AddressRequest("Country", "State", "ZipCode", uniqueCity, "Street"),
            new MoneyRequest(100.0m, "USD"),
            new MoneyRequest(50.0m, "USD"),
            []);

        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync("api/v1/apartments", request);
        createResponse.IsSuccessStatusCode.Should().BeTrue();

        var query = new SearchApartmentsQuery(
            null, null, uniqueCity, null, null, null, null, null, 1, 10);

        // Act
        Result<PagedResponse<ApartmentResponse>> result = await Sender.Send(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].AverageRating.Should().Be(0.0);
    }

    [Fact]
    public async Task SearchApartments_ShouldReturn_CorrectAverageRating_ForApartmentWithReviews()
    {
        // Arrange
        var reviewsToCreate = new List<(int Rating, string Comment)>
        {
            (3, "Okay stay"),
            (5, "Perfect stay")
        };

        // This creates an apartment, completes multiple bookings, and adds reviews for them
        var (apartmentId, _, _, _, guestToken, _) = await Bookings.BookingTestHelpers.SetupApartmentWithMultipleReviewedBookingsAsync(this, reviewsToCreate);

        // First we need to find the city of the apartment that was created by the helper to isolate it
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, guestToken);
        var getApartmentResponse = await HttpClient.GetAsync(new Uri($"api/v1/apartments/{apartmentId}", UriKind.Relative));
        getApartmentResponse.IsSuccessStatusCode.Should().BeTrue();

        var apartmentDetails = await getApartmentResponse.Content.ReadFromJsonAsync<Bookify.Application.Apartments.GetApartment.ApartmentResponse>();
        var city = apartmentDetails!.Address.City;

        var query = new SearchApartmentsQuery(
            null, null, city, null, null, null, null, null, 1, 100);

        // Act
        Result<PagedResponse<ApartmentResponse>> result = await Sender.Send(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        // Find our specific apartment since the city might be shared with others (from seed data or other tests)
        var apartment = result.Value.Items.SingleOrDefault(a => a.Id == apartmentId);
        apartment.Should().NotBeNull();

        // The average of 3 and 5 is 4.0
        apartment.AverageRating.Should().Be(4.0);
    }

}
