using DocMind.Domain.Common;
using FluentValidation;
using NetArchTest.Rules;

namespace DocMind.ArchitectureTests;

public sealed class ConventionTests
{
    [Fact]
    public void Handlers_AreSealed()
    {
        var result = Types.InAssembly(Assemblies.Load(Assemblies.Application))
            .That().HaveNameEndingWith("Handler", StringComparison.Ordinal)
            .Should().BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Handlers_LiveInTheApplicationLayer()
    {
        var infrastructureHandlers = Types.InAssemblies(
                [Assemblies.Load(Assemblies.Domain), Assemblies.Load(Assemblies.Infrastructure), Assemblies.Load(Assemblies.Api)])
            .That().HaveNameEndingWith("Handler", StringComparison.Ordinal)
            .And().AreClasses()
            .And().DoNotHaveName("GlobalExceptionHandler")
            .GetTypes();

        infrastructureHandlers.Should().BeEmpty();
    }

    [Fact]
    public void Validators_AreSealedAndNamedConsistently()
    {
        var validators = Assemblies.Load(Assemblies.Application).GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IValidator).IsAssignableFrom(t))
            .ToList();

        validators.Where(t => !t.IsSealed).Should().BeEmpty();
        validators.Where(t => !t.Name.EndsWith("Validator", StringComparison.Ordinal)).Should().BeEmpty();
    }

    [Fact]
    public void DomainEntities_ExposeNoPublicSetters()
    {
        var entities = Assemblies.Load(Assemblies.Domain).GetTypes()
            .Where(t => typeof(Entity).IsAssignableFrom(t) && !t.IsAbstract);

        var offenders = entities
            .SelectMany(t => t.GetProperties().Where(p => p.SetMethod is { IsPublic: true }).Select(p => $"{t.Name}.{p.Name}"))
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void DomainEntities_AreSealed()
    {
        var entities = Assemblies.Load(Assemblies.Domain).GetTypes()
            .Where(t => typeof(Entity).IsAssignableFrom(t) && !t.IsAbstract);

        entities.Should().OnlyContain(t => t.IsSealed);
    }

    [Fact]
    public void EndpointClasses_AreStaticAndInternal()
    {
        var result = Types.InAssembly(Assemblies.Load(Assemblies.Api))
            .That().HaveNameEndingWith("Endpoints", StringComparison.Ordinal)
            .Should().BeStatic().And().NotBePublic()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void EntityConfigurations_AreInternalAndSealed()
    {
        var result = Types.InAssembly(Assemblies.Load(Assemblies.Infrastructure))
            .That().HaveNameEndingWith("Configuration", StringComparison.Ordinal)
            .And().AreClasses()
            .Should().BeSealed().And().NotBePublic()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        result.FailingTypeNames is { Count: > 0 } names ? string.Join(", ", names) : string.Empty;
}
