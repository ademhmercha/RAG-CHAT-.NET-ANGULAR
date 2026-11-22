using Microsoft.Extensions.Configuration;

const string ChatModel = "llama3.2";
const string EmbeddingModel = "nomic-embed-text";

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg17")
    .WithDataVolume("docmind-postgres-data")
    .WithPgAdmin();

var database = postgres.AddDatabase("docmind");

var ollama = builder.AddOllama("ollama")
    .WithDataVolume("docmind-ollama-data");

ollama.AddModel("chat", ChatModel);
ollama.AddModel("embeddings", EmbeddingModel);

if (builder.Configuration.GetValue<bool>("Ollama:UseGpu"))
{
    ollama.WithGPUSupport();
}

var jwtSigningKey = builder.AddParameter(
    "jwt-signing-key",
    new GenerateParameterDefault { MinLength = 64, Special = false },
    secret: true,
    persist: true);

var storagePath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", ".data", "storage"));

var api = builder.AddDotnetProject("api", "../DocMind.Api/DocMind.Api.csproj")
    .WithReference(database)
    .WaitFor(database)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithEnvironment("Storage__Local__RootPath", storagePath)
    .WithEnvironment("Ai__Provider", "Ollama")
    .WithEnvironment("Ai__Ollama__Endpoint", ollama.GetEndpoint("http"))
    .WithEnvironment("Ai__Ollama__ChatModel", ChatModel)
    .WithEnvironment("Ai__Ollama__EmbeddingModel", EmbeddingModel)
    .WithHttpHealthCheck("/health/ready")
    .WithExternalHttpEndpoints();

builder.AddDotnetProject("worker", "../DocMind.Worker/DocMind.Worker.csproj")
    .WithReference(database)
    .WaitFor(api)
    .WithEnvironment("Storage__Local__RootPath", storagePath)
    .WithEnvironment("Ingestion__SweepIntervalSeconds", "3")
    .WithEnvironment("Ingestion__PendingGraceSeconds", "1")
    .WithEnvironment("Ai__Provider", "Ollama")
    .WithEnvironment("Ai__Ollama__Endpoint", ollama.GetEndpoint("http"))
    .WithEnvironment("Ai__Ollama__ChatModel", ChatModel)
    .WithEnvironment("Ai__Ollama__EmbeddingModel", EmbeddingModel);

builder.AddJavaScriptApp("web", "../../web/docmind-web", "start")
    .WithNpm(install: true)
    .WithHttpEndpoint(env: "PORT")
    .WithEnvironment("API_URL", api.GetEndpoint("http"))
    .WaitFor(api)
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();
