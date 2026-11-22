using DocMind.Application.Abstractions;
using DocMind.Domain.Common;
using DocMind.Domain.Conversations;
using Microsoft.EntityFrameworkCore;

namespace DocMind.Application.Features.Conversations;

public sealed class RenameConversationHandler(IAppDbContext db, TimeProvider time)
{
    public async Task<Result<ConversationSummaryDto>> HandleAsync(
        Guid userId,
        Guid conversationId,
        RenameConversationRequest request,
        CancellationToken cancellationToken)
    {
        var conversation = await db.Conversations
            .SingleOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId, cancellationToken);

        if (conversation is null)
        {
            return ConversationErrors.NotFound(conversationId);
        }

        conversation.Rename(request.Title, time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return conversation.ToSummary();
    }
}
