using DocMind.Api.Configuration;
using DocMind.Application;
using DocMind.Infrastructure;
using DocMind.Infrastructure.Auth;
using DocMind.ServiceDefaults;

const long DefaultMaxRequestBodyBytes = 1024 * 1024;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.AddServerHeader = false;
    kestrel.Limits.MaxRequestBodySize = DefaultMaxRequestBodyBytes;
    kestrel.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    kestrel.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
});

builder.AddServiceDefaults();
builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration)
    .AddDocumentEventStreaming(builder.Configuration)
    .AddIdentityInfrastructure(builder.Configuration)
    .AddApi(builder.Configuration);

var app = builder.Build();

await app.ApplyMigrationsIfConfiguredAsync();

app.UseApiPipeline();
app.MapDefaultEndpoints();
app.MapApiV1();
app.MapApiDocumentation();

await app.RunAsync();

public partial class Program;
