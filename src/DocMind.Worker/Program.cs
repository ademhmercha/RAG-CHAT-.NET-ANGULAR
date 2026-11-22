using DocMind.Application;
using DocMind.Infrastructure;
using DocMind.ServiceDefaults;
using DocMind.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration)
    .AddMaintenanceJobs();

builder.Services.AddHostedService<IngestionWorker>();
builder.Services.AddHostedService<RecoveryWorker>();

await builder.Build().RunAsync();
