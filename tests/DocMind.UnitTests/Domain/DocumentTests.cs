using DocMind.Domain.Documents;

namespace DocMind.UnitTests.Domain;

public sealed class DocumentTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    private static Document NewDocument() =>
        Document.Create(UserId, "  report.pdf  ", "application/pdf", 1024, "users/abc/report.pdf", Now);

    [Fact]
    public void Create_StartsPendingAndTrimsTheFileName()
    {
        var document = NewDocument();

        document.Id.Should().NotBeEmpty();
        document.Status.Should().Be(DocumentStatus.Pending);
        document.FileName.Should().Be("report.pdf");
        document.UserId.Should().Be(UserId);
        document.CreatedAt.Should().Be(Now);
        document.UpdatedAt.Should().Be(Now);
        document.ChunkCount.Should().Be(0);
    }

    [Fact]
    public void Create_GeneratesUuidVersion7Identifiers()
    {
        var first = NewDocument();
        var second = NewDocument();

        first.Id.Should().NotBe(second.Id);
        first.Id.Version.Should().Be(7);
    }

    [Fact]
    public void Create_RejectsAnEmptyUser()
    {
        var act = () => Document.Create(Guid.Empty, "a.pdf", "application/pdf", 1, "k", Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsABlankFileName(string fileName)
    {
        var act = () => Document.Create(UserId, fileName, "application/pdf", 1, "k", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void HappyPath_MovesFromPendingToReady()
    {
        var document = NewDocument();
        var later = Now.AddMinutes(1);

        document.MarkProcessing(later).IsSuccess.Should().BeTrue();
        document.Status.Should().Be(DocumentStatus.Processing);

        document.MarkReady(pageCount: 3, chunkCount: 12, later.AddMinutes(1)).IsSuccess.Should().BeTrue();

        document.Status.Should().Be(DocumentStatus.Ready);
        document.PageCount.Should().Be(3);
        document.ChunkCount.Should().Be(12);
        document.ErrorMessage.Should().BeNull();
        document.UpdatedAt.Should().Be(later.AddMinutes(1));
    }

    [Fact]
    public void MarkReady_RequiresProcessing()
    {
        var document = NewDocument();

        var result = document.MarkReady(1, 1, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("documents.invalid_transition");
        document.Status.Should().Be(DocumentStatus.Pending);
    }

    [Fact]
    public void MarkFailed_StoresTheMessageAndAllowsAnotherAttempt()
    {
        var document = NewDocument();
        document.MarkProcessing(Now);

        document.MarkFailed("extraction failed", Now.AddSeconds(5)).IsSuccess.Should().BeTrue();

        document.Status.Should().Be(DocumentStatus.Failed);
        document.ErrorMessage.Should().Be("extraction failed");

        document.MarkProcessing(Now.AddSeconds(10)).IsSuccess.Should().BeTrue();
        document.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_TruncatesVeryLongMessages()
    {
        var document = NewDocument();

        document.MarkFailed(new string('x', Document.ErrorMessageMaxLength + 50), Now);

        document.ErrorMessage.Should().HaveLength(Document.ErrorMessageMaxLength);
    }

    [Fact]
    public void ReadyDocuments_CannotBeReprocessedOrFailed()
    {
        var document = NewDocument();
        document.MarkProcessing(Now);
        document.MarkReady(1, 1, Now);

        document.MarkProcessing(Now).IsFailure.Should().BeTrue();
        document.MarkFailed("late failure", Now).IsFailure.Should().BeTrue();
        document.Status.Should().Be(DocumentStatus.Ready);
    }
}
