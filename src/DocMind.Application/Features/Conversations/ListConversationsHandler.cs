using DocMind.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace DocMind.Application.Features.Conversations;

public sealed class ListConversationsHandler(IAppDbContext db)
{
    public const int MaxResults = 200;

    public async Task<IReadOnlyList<ConversationSummaryDto>> HandleAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Conversations
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAt)
            .ThenByDescending(c => c.Id)
            .Take(MaxResults)
            .Select(c => new ConversationSummaryDto(c.Id, c.Title, c.CreatedAt, c.UpdatedAt))
            .ToListAsync(cancellationToken);
}
