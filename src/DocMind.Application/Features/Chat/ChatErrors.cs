using DocMind.Domain.Common;

namespace DocMind.Application.Features.Chat;

public static class ChatErrors
{
    public static readonly Error ProviderUnavailable =
        Error.Failure("chat.provider_unavailable", "The AI provider could not complete the request. Please try again.");

    public static readonly Error EmptyResponse =
        Error.Failure("chat.empty_response", "The AI provider returned an empty answer. Please try again.");

    public static readonly Error PersistenceFailed =
        Error.Failure("chat.persistence_failed", "The answer could not be saved. Please try again.");
}
