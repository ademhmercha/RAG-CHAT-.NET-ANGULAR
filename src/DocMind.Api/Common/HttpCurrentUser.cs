using DocMind.Application.Abstractions;

namespace DocMind.Api.Common;

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private const string SubjectClaim = "sub";

    public Guid? UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirst(SubjectClaim)?.Value, out var id) ? id : null;

    public Guid RequiredUserId =>
        UserId ?? throw new InvalidOperationException("No authenticated user is associated with the current request.");
}
