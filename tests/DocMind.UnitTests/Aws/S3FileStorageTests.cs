using System.Net;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using DocMind.Infrastructure.Aws;
using DocMind.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace DocMind.UnitTests.Aws;

public sealed class S3FileStorageTests
{
    private const string Bucket = "docmind-test";

    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();

    private S3FileStorage CreateStorage(string prefix = "") => new(
        _s3,
        Options.Create(new StorageOptions { Provider = StorageProviders.S3, S3 = new S3StorageOptions { BucketName = Bucket, Region = "eu-west-1", KeyPrefix = prefix } }));

    [Fact]
    public async Task Save_UploadsTheObjectEncryptedWithItsContentType()
    {
        PutObjectRequest? captured = null;
        string? uploaded = null;
        _s3.PutObjectAsync(Arg.Do<PutObjectRequest>(request =>
            {
                captured = request;
                uploaded = new StreamReader(request.InputStream, Encoding.UTF8, leaveOpen: true).ReadToEnd();
            }), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse());
        await using var content = new MemoryStream("pdf-bytes"u8.ToArray());

        await CreateStorage().SaveAsync("documents/u/d/original.pdf", content, "application/pdf", TestContext.Current.CancellationToken);

        captured.Should().NotBeNull();
        captured.BucketName.Should().Be(Bucket);
        captured.Key.Should().Be("documents/u/d/original.pdf");
        captured.ContentType.Should().Be("application/pdf");
        captured.ServerSideEncryptionMethod.Should().Be(ServerSideEncryptionMethod.AES256);
        captured.AutoCloseStream.Should().BeFalse();
        uploaded.Should().Be("pdf-bytes");
    }

    [Fact]
    public async Task Save_BuffersStreamsThatCannotSeek()
    {
        long? length = null;
        _s3.PutObjectAsync(Arg.Do<PutObjectRequest>(request => length = request.InputStream.Length), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse());
        await using var content = new NonSeekableStream("streamed"u8.ToArray());

        await CreateStorage().SaveAsync("a/b.bin", content, "application/octet-stream", TestContext.Current.CancellationToken);

        length.Should().Be(8);
    }

    [Fact]
    public async Task Save_PrefixesTheObjectKey()
    {
        PutObjectRequest? captured = null;
        _s3.PutObjectAsync(Arg.Do<PutObjectRequest>(request => captured = request), Arg.Any<CancellationToken>()).Returns(new PutObjectResponse());
        await using var content = new MemoryStream([1]);

        await CreateStorage("tenant-a/").SaveAsync("a/b.bin", content, "x", TestContext.Current.CancellationToken);

        captured!.Key.Should().Be("tenant-a/a/b.bin");
    }

    [Fact]
    public async Task Open_StreamsTheObjectAndReleasesTheResponseWhenDisposed()
    {
        var body = new TrackedStream("hello s3"u8.ToArray());
        _s3.GetObjectAsync(Bucket, "a/b.bin", Arg.Any<CancellationToken>())
            .Returns(new GetObjectResponse { ResponseStream = body, ContentLength = 8 });

        var stream = await CreateStorage().OpenReadAsync("a/b.bin", TestContext.Current.CancellationToken);
        using (var reader = new StreamReader(stream))
        {
            (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).Should().Be("hello s3");
        }

        body.Disposed.Should().BeTrue();
    }

    [Theory]
    [InlineData("NoSuchKey", HttpStatusCode.NotFound)]
    [InlineData("NotFound", HttpStatusCode.NotFound)]
    public async Task Open_TranslatesMissingObjectsToFileNotFound(string code, HttpStatusCode status)
    {
        _s3.GetObjectAsync(Bucket, "a/b.bin", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GetObjectResponse>(new AmazonS3Exception("missing") { ErrorCode = code, StatusCode = status }));

        var act = () => CreateStorage().OpenReadAsync("a/b.bin", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task Open_DoesNotHideOtherS3Failures()
    {
        _s3.GetObjectAsync(Bucket, "a/b.bin", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GetObjectResponse>(new AmazonS3Exception("denied") { ErrorCode = "AccessDenied", StatusCode = HttpStatusCode.Forbidden }));

        var act = () => CreateStorage().OpenReadAsync("a/b.bin", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<AmazonS3Exception>();
    }

    [Fact]
    public async Task Delete_RemovesTheObject()
    {
        _s3.DeleteObjectAsync(Bucket, "tenant/a/b.bin", Arg.Any<CancellationToken>()).Returns(new DeleteObjectResponse());

        await CreateStorage("tenant").DeleteAsync("a/b.bin", TestContext.Current.CancellationToken);

        await _s3.Received(1).DeleteObjectAsync(Bucket, "tenant/a/b.bin", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("/absolute")]
    [InlineData("a//b")]
    [InlineData("a\\b")]
    [InlineData("")]
    public async Task Operations_RejectUnsafeKeysWithoutCallingS3(string key)
    {
        var storage = CreateStorage();
        await using var content = new MemoryStream([1]);

        await storage.Invoking(s => s.SaveAsync(key, content, "x", TestContext.Current.CancellationToken)).Should().ThrowAsync<ArgumentException>();
        await storage.Invoking(s => s.OpenReadAsync(key, TestContext.Current.CancellationToken)).Should().ThrowAsync<ArgumentException>();
        await storage.Invoking(s => s.DeleteAsync(key, TestContext.Current.CancellationToken)).Should().ThrowAsync<ArgumentException>();

        _s3.ReceivedCalls().Should().BeEmpty();
    }

    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }

    private sealed class TrackedStream(byte[] data) : MemoryStream(data)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
