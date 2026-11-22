using DocMind.Application.Abstractions;
using DocMind.Domain.Common;
using DocMind.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace DocMind.Application.Features.Documents;

public sealed class GetDocumentHandler(IAppDbContext db)
{
    public async Task<Result<DocumentDto>> HandleAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await db.Documents
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == documentId && d.UserId == userId, cancellationToken);

        return document is null ? DocumentErrors.NotFound(documentId) : document.ToDto();
    }
}
