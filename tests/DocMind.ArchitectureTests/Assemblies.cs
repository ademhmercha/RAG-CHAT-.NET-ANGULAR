using System.Reflection;

namespace DocMind.ArchitectureTests;

internal static class Assemblies
{
    public const string Domain = "DocMind.Domain";
    public const string Application = "DocMind.Application";
    public const string Infrastructure = "DocMind.Infrastructure";
    public const string Api = "DocMind.Api";
    public const string Worker = "DocMind.Worker";
    public const string McpServer = "DocMind.McpServer";

    public static Assembly Load(string name) => Assembly.Load(name);

    public static IReadOnlyList<string> ReferencedNames(string name) =>
        Load(name).GetReferencedAssemblies().Select(a => a.Name!).ToList();
}
