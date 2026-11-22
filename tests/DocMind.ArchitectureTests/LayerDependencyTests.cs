namespace DocMind.ArchitectureTests;

public sealed class LayerDependencyTests
{
    private static readonly string[] Hosts = [Assemblies.Api, Assemblies.Worker, Assemblies.McpServer];

    private static readonly string[] ProviderSdks =
    [
        "Npgsql",
        "Pgvector.EntityFrameworkCore",
        "OllamaSharp",
        "AWSSDK",
        "AWS.Bedrock",
        "Microsoft.Extensions.AI.Ollama",
        "Microsoft.AspNetCore",
    ];

    private static readonly string[] DomainAllowedPrefixes = ["System", "netstandard", "mscorlib", "Pgvector"];

    [Fact]
    public void Domain_DependsOnlyOnTheBaseClassLibraryAndThePgvectorDataType()
    {
        var offenders = Assemblies.ReferencedNames(Assemblies.Domain)
            .Where(name => !DomainAllowedPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void Domain_DoesNotReferenceEntityFrameworkOrAspNet()
    {
        var references = Assemblies.ReferencedNames(Assemblies.Domain);

        references.Should().NotContain(n => n.StartsWith("Microsoft.", StringComparison.Ordinal));
        references.Should().NotContain(n => n.StartsWith("Npgsql", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrHosts()
    {
        var references = Assemblies.ReferencedNames(Assemblies.Application);

        references.Should().NotContain(Assemblies.Infrastructure);
        references.Should().NotContain(Hosts);
    }

    [Fact]
    public void Application_DoesNotDependOnAnyProviderSdk()
    {
        var offenders = Assemblies.ReferencedNames(Assemblies.Application)
            .Where(name => ProviderSdks.Any(sdk => name.StartsWith(sdk, StringComparison.Ordinal)))
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnHosts()
    {
        Assemblies.ReferencedNames(Assemblies.Infrastructure).Should().NotContain(Hosts);
    }

    [Theory]
    [InlineData(Assemblies.Api, Assemblies.Worker, Assemblies.McpServer)]
    [InlineData(Assemblies.Worker, Assemblies.Api, Assemblies.McpServer)]
    [InlineData(Assemblies.McpServer, Assemblies.Api, Assemblies.Worker)]
    public void Hosts_DoNotDependOnEachOther(string host, string otherHost, string anotherHost)
    {
        var references = Assemblies.ReferencedNames(host);

        references.Should().NotContain(otherHost);
        references.Should().NotContain(anotherHost);
    }

    [Theory]
    [InlineData(Assemblies.Api)]
    [InlineData(Assemblies.Worker)]
    [InlineData(Assemblies.McpServer)]
    public void Hosts_DoNotReferenceCloudOrModelProviderSdksDirectly(string host)
    {
        var offenders = Assemblies.ReferencedNames(host)
            .Where(name => name.StartsWith("AWSSDK", StringComparison.Ordinal)
                || name.StartsWith("AWS.", StringComparison.Ordinal)
                || name.StartsWith("OllamaSharp", StringComparison.Ordinal))
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void Api_DoesNotTouchEntityFrameworkOrTheDatabaseDriverDirectly()
    {
        var references = Assemblies.ReferencedNames(Assemblies.Api);

        references.Should().NotContain(n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        references.Should().NotContain(n => n.StartsWith("Npgsql", StringComparison.Ordinal));
    }
}
