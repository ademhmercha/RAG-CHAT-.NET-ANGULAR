using System.Text;
using DocMind.Domain.Conversations;
using Microsoft.Extensions.AI;

namespace DocMind.Application.Features.Chat;

public sealed class ChatStreamProcessor
{
    private readonly StringBuilder _text = new();
    private readonly Dictionary<string, string> _pendingCalls = new(StringComparer.Ordinal);
    private long _inputTokens;
    private long _outputTokens;
    private bool _usageReported;

    public string Text => _text.ToString();

    public bool HasText => _text.Length > 0;

    public TokenUsage? Usage => _usageReported
        ? new TokenUsage((int)Math.Min(_inputTokens, int.MaxValue), (int)Math.Min(_outputTokens, int.MaxValue))
        : null;

    public IReadOnlyList<ChatEvent> Process(ChatResponseUpdate update)
    {
        var events = new List<ChatEvent>();

        foreach (var content in update.Contents)
        {
            switch (content)
            {
                case TextContent { Text.Length: > 0 } text:
                    _text.Append(text.Text);
                    events.Add(new TokenChatEvent(text.Text));
                    break;

                case FunctionCallContent call:
                    _pendingCalls[call.CallId] = call.Name;
                    events.Add(new ToolChatEvent(call.Name, ToolStatus.Running));
                    break;

                case FunctionResultContent result:
                    var name = _pendingCalls.GetValueOrDefault(result.CallId, "tool");
                    events.Add(new ToolChatEvent(name, result.Exception is null ? ToolStatus.Completed : ToolStatus.Failed));
                    break;

                case UsageContent usage:
                    _usageReported = true;
                    _inputTokens += usage.Details.InputTokenCount ?? 0;
                    _outputTokens += usage.Details.OutputTokenCount ?? 0;
                    break;
            }
        }

        return events;
    }
}
