namespace DocMind.IntegrationTests.Infrastructure;

[CollectionDefinition(PostgresFixture.CollectionName)]
public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresFixture>;
