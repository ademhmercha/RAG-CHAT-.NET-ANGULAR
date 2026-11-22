using Amazon.S3;
using Amazon.S3.Model;
using DocMind.Application.Abstractions;
using DocMind.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace DocMind.Infrastructure.Aws;

internal sealed class S3FileStorage(IAmazonS3 s3, IOptions<StorageOptions> options) : IFileStorage
{
    private const string NoSuchKey = "NoSuchKey";
    private const string NotFound = "NotFound";

    private readonly S3StorageOptions _options = options.Value.S3;

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var objectKey = ObjectKey(key);
        var buffered = content.CanSeek ? null : new MemoryStream();

        try
        {
            if (buffered is not null)
            {
                await content.CopyToAsync(buffered, cancellationToken);
                buffered.Seek(0, SeekOrigin.Begin);
            }

            await s3.PutObjectAsync(
                new PutObjectRequest
                {
                    BucketName = _options.BucketName,
                    Key = objectKey,
                    InputStream = buffered ?? content,
                    ContentType = contentType,
                    AutoCloseStream = false,
                    ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
                },
                cancellationToken);
        }
        finally
        {
            if (buffered is not null)
            {
                await buffered.DisposeAsync();
            }
        }
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await s3.GetObjectAsync(_options.BucketName, ObjectKey(key), cancellationToken);
            return new ResponseOwningStream(response);
        }
        catch (AmazonS3Exception exception) when (IsMissing(exception))
        {
            throw new FileNotFoundException("The stored file was not found.", key, exception);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        await s3.DeleteObjectAsync(_options.BucketName, ObjectKey(key), cancellationToken);

    private static bool IsMissing(AmazonS3Exception exception) =>
        exception.StatusCode == System.Net.HttpStatusCode.NotFound
        || string.Equals(exception.ErrorCode, NoSuchKey, StringComparison.Ordinal)
        || string.Equals(exception.ErrorCode, NotFound, StringComparison.Ordinal);

    private string ObjectKey(string key)
    {
        _ = StorageKey.Segments(key);
        return string.IsNullOrEmpty(_options.KeyPrefix) ? key : $"{_options.KeyPrefix.Trim('/')}/{key}";
    }

    private sealed class ResponseOwningStream(GetObjectResponse response) : Stream
    {
        private readonly Stream _inner = response.ResponseStream;

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => response.ContentLength;

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                response.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
