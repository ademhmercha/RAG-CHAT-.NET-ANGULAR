using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;

namespace DocMind.IntegrationTests.Infrastructure;

public sealed class FakeEmbeddingGenerator(int dimensions = TestDatabase.Dimensions) : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly Lock _gate = new();
    private readonly List<string> _inputs = [];
    private readonly List<int> _batchSizes = [];

    public Func<IEnumerable<string>, Exception?>? FailWith { get; set; }

    public IReadOnlyList<int> BatchSizes
    {
        get
        {
            lock (_gate)
            {
                return [.. _batchSizes];
            }
        }
    }

    public IReadOnlyList<string> Inputs
    {
        get
        {
            lock (_gate)
            {
                return [.. _inputs];
            }
        }
    }

    public int Dimensions { get; set; } = dimensions;

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var inputs = values.ToList();

        lock (_gate)
        {
            _inputs.AddRange(inputs);
            _batchSizes.Add(inputs.Count);
        }

        var failure = FailWith?.Invoke(inputs);
        if (failure is not null)
        {
            return Task.FromException<GeneratedEmbeddings<Embedding<float>>>(failure);
        }

        var embeddings = new GeneratedEmbeddings<Embedding<float>>(inputs.Select(i => new Embedding<float>(Embed(i, Dimensions))));
        return Task.FromResult(embeddings);
    }

    public static float[] Embed(string text, int dimensions)
    {
        var vector = new float[dimensions];

        foreach (var word in text.ToLowerInvariant().Split([' ', '\n', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries))
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(word));
            var index = BitConverter.ToUInt32(hash, 0) % (uint)dimensions;
            vector[index] += 1f;
        }

        return vector;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
