namespace RentConnect.IntegrationTests;

[CollectionDefinition(
    Name,
    DisableParallelization = true)]
public sealed class SqlServerIntegrationCollection
{
    public const string Name =
        "SQL Server integration";
}
