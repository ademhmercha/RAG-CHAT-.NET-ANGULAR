using DocMind.Application.Abstractions;
using DocMind.Domain.Common;
using DocMind.Domain.Conversations;

namespace DocMind.Application.Features.Conversations;

public sealed class CreateConversationHandler(IAppDbContext db, TimeProvider time)
{
    public async Task<Result<ConversationSummaryDto>> HandleAsync(
        Guid userId,
        CreateConversationRequest? request,
        CancellationToken cancellationToken)
    {
        var conversation = Conversation.Create(userId, request?.Title, time.GetUtcNow());

        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(cancellationToken);

        return conversation.ToSummary();
    }
}
