using Bookify.Application.IntegrationTests.Infrastructure;

namespace Bookify.Application.IntegrationTests.Abstractions;

[CollectionDefinition("IntegrationTests")]
public class IntegrationTest : ICollectionFixture<IntegrationTestWebAppFactory>
{
}