using Bookify.Application.IntegrationTests.Infrastructure;

namespace Bookify.Application.IntegrationTests.Abstractions;

[CollectionDefinition("RateLimitTestCollection")]
public class RateLimitTestDefinition : ICollectionFixture<RateLimitTestWebAppFactory>;