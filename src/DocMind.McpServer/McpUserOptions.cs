using System.ComponentModel.DataAnnotations;

namespace DocMind.McpServer;

public sealed class McpUserOptions : IValidatableObject
{
    public const string SectionName = "McpServer";

    public Guid UserId { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (UserId == Guid.Empty)
        {
            yield return new ValidationResult(
                "McpServer:UserId must be the identifier of the DocMind user whose documents this server exposes.",
                [nameof(UserId)]);
        }
    }
}
