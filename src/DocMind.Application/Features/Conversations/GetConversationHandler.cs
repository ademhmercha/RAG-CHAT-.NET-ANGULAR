using DocMind.Application.Abstractions;
using DocMind.Domain.Common;
using DocMind.Domain.Conversations;
using Microsoft.EntityFrameworkCore;

namespace DocMind.Application.Features.Conversations;

public sealed class GetConversationHandler(IAppDbContext db)
{
    public async Task<Result<ConversationDto>> HandleAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await db.Conversations
            .AsNoTracking()
            .Include(c => c.Messages.OrderBy(m => EF.Property<long>(m, MessageOrdering.Column)))
            .SingleOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId, cancellationToken);

        return conversation is null ? ConversationErrors.NotFound(conversationId) : conversation.ToDto();
    }
}
