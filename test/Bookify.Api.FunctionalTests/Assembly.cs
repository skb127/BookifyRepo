

// Disable parallelization for functional tests since starting multiple Keycloak/Postgres containers concurrently is extremely resource-heavy.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
