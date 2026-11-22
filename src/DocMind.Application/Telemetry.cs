using System.Diagnostics;

namespace DocMind.Application;

public static class Telemetry
{
    public const string SourceName = "DocMind";

    public static readonly ActivitySource Source = new(SourceName);
}
