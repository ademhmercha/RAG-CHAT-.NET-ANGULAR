namespace DocMind.Domain.Conversations;

public sealed record TokenUsage(int InputTokens, int OutputTokens)
{
    public int TotalTokens => InputTokens + OutputTokens;
}
