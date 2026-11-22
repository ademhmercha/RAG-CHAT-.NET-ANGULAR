using System.ComponentModel.DataAnnotations;

namespace DocMind.Application.Features.Chat;

public sealed class ChatSettings
{
    public const string SectionName = "Chat";

    [Range(1, 100)]
    public int HistoryMessages { get; init; } = 20;

    [Range(0.0, 2.0)]
    public float Temperature { get; init; } = 0.2f;

    [Range(64, 16384)]
    public int MaxOutputTokens { get; init; } = 1024;

    [Range(1, 20)]
    public int MaxToolIterations { get; init; } = 5;

    public bool ToolsEnabled { get; init; } = true;
}
