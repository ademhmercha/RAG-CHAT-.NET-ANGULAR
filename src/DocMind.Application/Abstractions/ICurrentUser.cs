namespace DocMind.Application.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }

    Guid RequiredUserId { get; }
}
