using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Application.IntegrationTests.Infrastructure;
using Bookify.Application.IntegrationTests.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Bookify.Application.IntegrationTests.Apartments;

public class ApartmentCacheInvalidationTests : BaseIntegrationTest
{
    public ApartmentCacheInvalidationTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task UpdateApartment_ShouldServeUpdatedData_AfterCacheIsInvalidated()
    {
        // Arrange / Act
        string adminEmail = UserData.CacheInvalidationAdminUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);

        string accessToken = await GetAccessToken(adminEmail, UserData.CacheInvalidationAdminUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, accessToken);

        // 1. Create an apartment
        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync(
            "api/v1/apartments", ApartmentData.ValidCreateApartmentRequest);
        createResponse.EnsureSuccessStatusCode();
        Uri locationUri = createResponse.Headers.Location!;

        // 2. GET the apartment to populate the cache (cache miss → stored in Redis)
        HttpResponseMessage firstGetResponse = await HttpClient.GetAsync(locationUri);
        firstGetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var cachedApartment = await firstGetResponse.Content
            .ReadFromJsonAsync<Application.Apartments.GetApartment.ApartmentResponse>();
        cachedApartment!.Name.Should().Be(ApartmentData.ValidCreateApartmentRequest.Name);

        // 3. Update the apartment (triggers ApartmentUpdatedDomainEvent → Outbox → cache invalidation)
        HttpResponseMessage updateResponse = await HttpClient.PutAsJsonAsync(
            locationUri, ApartmentData.ValidUpdateApartmentRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 4. Wait for the Outbox processor to fire the domain event and invalidate the cache
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Assert
        // GET again — should hit a cache miss (invalidated) and return updated data from DB
        HttpResponseMessage secondGetResponse = await HttpClient.GetAsync(locationUri);
        secondGetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var freshApartment = await secondGetResponse.Content
            .ReadFromJsonAsync<Application.Apartments.GetApartment.ApartmentResponse>();
        freshApartment.Should().NotBeNull();
        freshApartment.Name.Should().Be(ApartmentData.ValidUpdateApartmentRequest.Name);
        freshApartment.Description.Should().Be(ApartmentData.ValidUpdateApartmentRequest.Description);
        freshApartment.Price.Amount.Should().Be(ApartmentData.ValidUpdateApartmentRequest.Price.Amount);
        freshApartment.Price.Currency.Should().Be(ApartmentData.ValidUpdateApartmentRequest.Price.Currency);
    }

    [Fact]
    public async Task DeleteApartment_ShouldReturn404_AfterCacheIsInvalidated()
    {
        // Arrange / Act
        // authenticate as Admin
        string adminEmail = UserData.CacheInvalidationAdminUserRequest.Email;
        await PromoteToAdminAsync(adminEmail);

        string accessToken = await GetAccessToken(adminEmail, UserData.CacheInvalidationAdminUserRequest.Password);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme, accessToken);

        // 1. Create an apartment
        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync(
            "api/v1/apartments", ApartmentData.ValidCreateApartmentRequest);
        createResponse.EnsureSuccessStatusCode();
        Uri locationUri = createResponse.Headers.Location!;

        // 2. GET to populate the cache
        HttpResponseMessage firstGetResponse = await HttpClient.GetAsync(locationUri);
        firstGetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Delete the apartment (triggers ApartmentDeletedDomainEvent → Outbox → cache invalidation)
        HttpResponseMessage deleteResponse = await HttpClient.DeleteAsync(locationUri);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 4. Wait for Outbox processor to fire the event and invalidate cache
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Assert
        // GET again — cache is invalidated, DB returns not found → 404
        HttpResponseMessage secondGetResponse = await HttpClient.GetAsync(locationUri);
        secondGetResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        string content = await secondGetResponse.Content.ReadAsStringAsync();
        content.Should().Contain("Apartment.NotFound");
    }
}
