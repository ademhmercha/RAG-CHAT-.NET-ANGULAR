using DocMind.Application.Abstractions;
using DocMind.Domain.Common;
using DocMind.Domain.Conversations;
using Microsoft.EntityFrameworkCore;

namespace DocMind.Application.Features.Conversations;

public sealed class DeleteConversationHandler(IAppDbContext db)
{
    public async Task<Result> HandleAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var deleted = await db.Conversations
            .Where(c => c.Id == conversationId && c.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? Result.Failure(ConversationErrors.NotFound(conversationId)) : Result.Success();
    }
}
