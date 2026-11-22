using DocMind.Application.Abstractions;
using DocMind.Domain.Documents;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DocMind.Application.Features.Documents;

public sealed record ListDocumentsRequest(int Page = 1, int PageSize = 20, string? Status = null)
{
    public DocumentStatus? ParsedStatus =>
        Enum.TryParse<DocumentStatus>(Status, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
}

public sealed class ListDocumentsRequestValidator : AbstractValidator<ListDocumentsRequest>
{
    public ListDocumentsRequestValidator(IOptions<DocumentSettings> settings)
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, settings.Value.MaxPageSize);
        RuleFor(x => x.Status)
            .Must(status => Enum.TryParse<DocumentStatus>(status, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage($"Status must be one of: {string.Join(", ", Enum.GetNames<DocumentStatus>())}.");
    }
}

public sealed class ListDocumentsHandler(IAppDbContext db)
{
    public async Task<PagedResult<DocumentDto>> HandleAsync(Guid userId, ListDocumentsRequest request, CancellationToken cancellationToken)
    {
        var query = db.Documents.AsNoTracking().Where(d => d.UserId == userId);

        if (request.ParsedStatus is { } status)
        {
            query = query.Where(d => d.Status == status);
        }

        var total = await query.LongCountAsync(cancellationToken);

        var documents = await query
            .OrderByDescending(d => d.CreatedAt)
            .ThenByDescending(d => d.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<DocumentDto>(documents.Select(d => d.ToDto()).ToList(), request.Page, request.PageSize, total);
    }
}
