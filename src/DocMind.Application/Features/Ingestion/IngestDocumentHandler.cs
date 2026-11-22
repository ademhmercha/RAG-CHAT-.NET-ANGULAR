using System.Diagnostics;
using DocMind.Application.Abstractions;
using DocMind.Application.Ai;
using DocMind.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using Polly;
using Polly.Retry;

namespace DocMind.Application.Features.Ingestion;

public enum IngestionOutcome
{
    Completed,
    Skipped,
    Failed,
}

public sealed class IngestDocumentHandler
{
    private const string TransientFailureMessage = "Processing failed because a dependent service was unavailable. Please upload the document again later.";
    private const string UnexpectedFailureMessage = "An unexpected error occurred while processing the document.";

    private readonly IAppDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IDocumentTextExtractor _extractor;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddings;
    private readonly IDocumentStatusNotifier _notifier;
    private readonly TimeProvider _time;
    private readonly IngestionSettings _settings;
    private readonly int _dimensions;
    private readonly ILogger<IngestDocumentHandler> _logger;
    private readonly ResiliencePipeline _retry;

    public IngestDocumentHandler(
        IAppDbContext db,
        IFileStorage storage,
        IDocumentTextExtractor extractor,
        IEmbeddingGenerator<string, Embedding<float>> embeddings,
        IDocumentStatusNotifier notifier,
        TimeProvider time,
        IOptions<IngestionSettings> settings,
        IOptions<EmbeddingOptions> embeddingOptions,
        ILogger<IngestDocumentHandler> logger)
    {
        _db = db;
        _storage = storage;
        _extractor = extractor;
        _embeddings = embeddings;
        _notifier = notifier;
        _time = time;
        _settings = settings.Value;
        _dimensions = embeddingOptions.Value.Dimensions;
        _logger = logger;
        _retry = BuildRetryPipeline(_settings, logger);
    }

    public async Task<IngestionOutcome> HandleAsync(Guid documentId, CancellationToken cancellationToken)
    {
        using var activity = Telemetry.Source.StartActivity("ingest-document");
        activity?.SetTag("document.id", documentId);

        if (!await ClaimAsync(documentId, cancellationToken))
        {
            return IngestionOutcome.Skipped;
        }

        var document = await _db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == documentId, cancellationToken);
        if (document is null)
        {
            return IngestionOutcome.Skipped;
        }

        await NotifyAsync(document.Id, document.UserId, DocumentStatus.Processing, null, cancellationToken);

        try
        {
            var completed = await ProcessAsync(document, cancellationToken);
            return completed ? IngestionOutcome.Completed : IngestionOutcome.Skipped;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await ReleaseAsync(documentId);
            throw;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            await FailAsync(document, MessageFor(exception), exception, cancellationToken);
            return IngestionOutcome.Failed;
        }
    }

    public async Task FailAsync(Guid documentId, string message, CancellationToken cancellationToken)
    {
        var document = await _db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == documentId, cancellationToken);
        if (document is not null)
        {
            await FailAsync(document, message, null, cancellationToken);
        }
    }

    private static ResiliencePipeline BuildRetryPipeline(IngestionSettings settings, ILogger logger) =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = settings.MaxAttempts - 1,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(settings.RetryBaseDelayMilliseconds),
                ShouldHandle = new PredicateBuilder().Handle<TransientProcessingException>(),
                OnRetry = args =>
                {
                    logger.LogWarning(args.Outcome.Exception, "Embedding attempt {Attempt} failed; retrying in {Delay}.", args.AttemptNumber + 1, args.RetryDelay);
                    return default;
                },
            })
            .Build();

    private static string MessageFor(Exception exception) => exception switch
    {
        UnsupportedDocumentException unsupported => unsupported.Message,
        TransientProcessingException => TransientFailureMessage,
        _ => UnexpectedFailureMessage,
    };

    private async Task<bool> ClaimAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var staleBefore = now - TimeSpan.FromMinutes(_settings.LeaseMinutes);

        var claimed = await _db.Documents
            .Where(d => d.Id == documentId
                && (d.Status == DocumentStatus.Pending || (d.Status == DocumentStatus.Processing && d.UpdatedAt < staleBefore)))
            .ExecuteUpdateAsync(
                s => s.SetProperty(d => d.Status, DocumentStatus.Processing)
                    .SetProperty(d => d.ErrorMessage, (string?)null)
                    .SetProperty(d => d.UpdatedAt, now),
                cancellationToken);

        return claimed > 0;
    }

    private async Task<bool> ProcessAsync(Document document, CancellationToken cancellationToken)
    {
        await using var content = await _storage.OpenReadAsync(document.StorageKey, cancellationToken);
        var extracted = await _extractor.ExtractAsync(content, document.ContentType, cancellationToken);

        var chunks = TextChunker.Chunk(extracted.Pages, _settings.ChunkSize, _settings.ChunkOverlap);
        if (chunks.Count == 0)
        {
            throw new UnsupportedDocumentException("The document contains no extractable text. Scanned documents are not supported.");
        }

        var vectors = await EmbedAsync(chunks, cancellationToken);
        var persisted = await PersistAsync(document, extracted.PageCount, chunks, vectors, cancellationToken);

        if (persisted)
        {
            await NotifyAsync(document.Id, document.UserId, DocumentStatus.Ready, null, cancellationToken);
        }

        return persisted;
    }

    private async Task<List<float[]>> EmbedAsync(IReadOnlyList<TextChunk> chunks, CancellationToken cancellationToken)
    {
        var vectors = new List<float[]>(chunks.Count);

        foreach (var batch in chunks.Chunk(_settings.EmbeddingBatchSize))
        {
            var texts = batch.Select(c => c.Text).ToList();
            var generated = await _retry.ExecuteAsync(
                async token => await EmbedBatchAsync(texts, token),
                cancellationToken);

            vectors.AddRange(generated);
        }

        return vectors;
    }

    private async Task<List<float[]>> EmbedBatchAsync(List<string> texts, CancellationToken cancellationToken)
    {
        GeneratedEmbeddings<Embedding<float>> generated;

        try
        {
            generated = await _embeddings.GenerateAsync(texts, cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TransientProcessingException("The embedding provider failed.", exception);
        }

        if (generated.Count != texts.Count)
        {
            throw new EmbeddingMismatchException($"The embedding provider returned {generated.Count} vectors for {texts.Count} inputs.");
        }

        var result = new List<float[]>(generated.Count);
        foreach (var vector in generated.Select(embedding => embedding.Vector))
        {
            if (vector.Length != _dimensions)
            {
                throw new EmbeddingMismatchException(
                    $"The embedding model returned {vector.Length} dimensions but {_dimensions} are configured (Embedding:Dimensions).");
            }

            result.Add(vector.ToArray());
        }

        return result;
    }

    private async Task<bool> PersistAsync(
        Document document,
        int pageCount,
        IReadOnlyList<TextChunk> chunks,
        List<float[]> vectors,
        CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var strategy = _db.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                _db.ChangeTracker.Clear();
                await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

                var ready = await _db.Documents
                    .Where(d => d.Id == document.Id && d.Status == DocumentStatus.Processing)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(d => d.Status, DocumentStatus.Ready)
                            .SetProperty(d => d.PageCount, pageCount)
                            .SetProperty(d => d.ChunkCount, chunks.Count)
                            .SetProperty(d => d.ErrorMessage, (string?)null)
                            .SetProperty(d => d.UpdatedAt, now),
                        cancellationToken);

                if (ready == 0)
                {
                    return false;
                }

                await _db.DocumentChunks.Where(c => c.DocumentId == document.Id).ExecuteDeleteAsync(cancellationToken);

                _db.DocumentChunks.AddRange(chunks.Select((chunk, index) =>
                    DocumentChunk.Create(document.Id, document.UserId, chunk.Index, chunk.Page, chunk.Text, new Vector(vectors[index]), now)));

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return true;
            });
        }
        catch (DbUpdateException)
        {
            if (await _db.Documents.AnyAsync(d => d.Id == document.Id, cancellationToken))
            {
                throw;
            }

            return false;
        }
    }

    private async Task FailAsync(Document document, string message, Exception? exception, CancellationToken cancellationToken)
    {
        if (exception is not null)
        {
            _logger.LogError(exception, "Ingestion of document {DocumentId} failed: {Message}", document.Id, message);
        }

        var now = _time.GetUtcNow();
        var text = message.Length > Document.ErrorMessageMaxLength ? message[..Document.ErrorMessageMaxLength] : message;

        var failed = await _db.Documents
            .Where(d => d.Id == document.Id && (d.Status == DocumentStatus.Processing || d.Status == DocumentStatus.Pending))
            .ExecuteUpdateAsync(
                s => s.SetProperty(d => d.Status, DocumentStatus.Failed)
                    .SetProperty(d => d.ErrorMessage, text)
                    .SetProperty(d => d.UpdatedAt, now),
                cancellationToken);

        if (failed > 0)
        {
            await NotifyAsync(document.Id, document.UserId, DocumentStatus.Failed, text, cancellationToken);
        }
    }

    private async Task ReleaseAsync(Guid documentId)
    {
        try
        {
            var now = _time.GetUtcNow();
            await _db.Documents
                .Where(d => d.Id == documentId && d.Status == DocumentStatus.Processing)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(d => d.Status, DocumentStatus.Pending).SetProperty(d => d.UpdatedAt, now),
                    CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Releasing document {DocumentId} after cancellation failed; the lease will expire.", documentId);
        }
    }

    private async Task NotifyAsync(Guid documentId, Guid userId, DocumentStatus status, string? error, CancellationToken cancellationToken)
    {
        try
        {
            await _notifier.NotifyAsync(new DocumentStatusChanged(documentId, userId, status, error, _time.GetUtcNow()), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Publishing the {Status} notification for document {DocumentId} failed.", status, documentId);
        }
    }
}
