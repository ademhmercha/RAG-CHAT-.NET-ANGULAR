using DocMind.Infrastructure.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DocMind.UnitTests.Infrastructure;

public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), $"docmind-tests-{Guid.NewGuid():N}");
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        Directory.CreateDirectory(_contentRoot);
        var environment = Substitute.For<IHostEnvironment>();
        environment.ContentRootPath.Returns(_contentRoot);

        _storage = new LocalFileStorage(
            Options.Create(new StorageOptions { Local = new LocalStorageOptions { RootPath = "files" } }),
            environment);
    }

    public void Dispose() => Directory.Delete(_contentRoot, recursive: true);

    private static MemoryStream Content(string text) => new(System.Text.Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task SaveAndOpen_RoundTripTheContent()
    {
        await _storage.SaveAsync("documents/user/doc/original.pdf", Content("hello"), "application/pdf", TestContext.Current.CancellationToken);

        await using var stream = await _storage.OpenReadAsync("documents/user/doc/original.pdf", TestContext.Current.CancellationToken);
        using var reader = new StreamReader(stream);
        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).Should().Be("hello");
    }

    [Fact]
    public async Task Save_OverwritesAndLeavesNoTemporaryFiles()
    {
        await _storage.SaveAsync("a/b.bin", Content("first"), "x", TestContext.Current.CancellationToken);
        await _storage.SaveAsync("a/b.bin", Content("second"), "x", TestContext.Current.CancellationToken);

        await using var stream = await _storage.OpenReadAsync("a/b.bin", TestContext.Current.CancellationToken);
        using var reader = new StreamReader(stream);
        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).Should().Be("second");
        Directory.GetFiles(Path.Combine(_contentRoot, "files", "a")).Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_RemovesTheFileAndEmptyParentFolders()
    {
        await _storage.SaveAsync("users/u1/doc1/original.pdf", Content("x"), "x", TestContext.Current.CancellationToken);

        await _storage.DeleteAsync("users/u1/doc1/original.pdf", TestContext.Current.CancellationToken);

        Directory.Exists(Path.Combine(_contentRoot, "files", "users")).Should().BeFalse();
        Directory.Exists(Path.Combine(_contentRoot, "files")).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_IsIdempotent()
    {
        var act = () => _storage.DeleteAsync("missing/file.bin", TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Open_ThrowsForAMissingFile()
    {
        var act = () => _storage.OpenReadAsync("missing/file.bin", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("a/../../outside.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/Windows/win.ini")]
    [InlineData("a\\b.txt")]
    [InlineData("a//b.txt")]
    [InlineData("")]
    [InlineData("a/./b.txt")]
    public async Task Operations_RejectKeysThatCouldEscapeTheRoot(string key)
    {
        var save = () => _storage.SaveAsync(key, Content("x"), "x", TestContext.Current.CancellationToken);
        var open = () => _storage.OpenReadAsync(key, TestContext.Current.CancellationToken);
        var delete = () => _storage.DeleteAsync(key, TestContext.Current.CancellationToken);

        await save.Should().ThrowAsync<ArgumentException>();
        await open.Should().ThrowAsync<ArgumentException>();
        await delete.Should().ThrowAsync<ArgumentException>();
    }
}
