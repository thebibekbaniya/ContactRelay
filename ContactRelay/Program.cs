using ContactRelay;
using Microsoft.Graph;
using ContactRelay.Data;
using ContactRelay.Graph;
using ContactRelay.Logging;
using ContactRelay.Mapping;
using ContactRelay.Options;
using ContactRelay.Services;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = builder.Configuration.GetValue<string>("Service:Name")
        ?? throw new InvalidOperationException("Service:Name is required.");
});

builder.Services.AddOptions<ServiceOptions>()
    .Bind(builder.Configuration.GetSection(ServiceOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ServiceOptions>, ServiceOptionsValidator>();

builder.Services.AddOptions<GraphOptions>()
    .Bind(builder.Configuration.GetSection(GraphOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<GraphOptions>, GraphOptionsValidator>();

builder.Services.AddOptions<SyncWorkerOptions>()
    .Bind(builder.Configuration.GetSection(SyncWorkerOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<SyncWorkerOptions>, SyncWorkerOptionsValidator>();

builder.Services.AddOptions<SqlOptions>()
    .Bind(builder.Configuration.GetSection(SqlOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<SqlOptions>, SqlOptionsValidator>();

builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddSingleton<IGraphClientFactory, ContactRelay.Graph.GraphClientFactory>();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IGraphClientFactory>().CreateClient());
builder.Services.AddSingleton<IGraphRetryHandler, GraphRetryHandler>();
builder.Services.AddSingleton<IDirectoryGraphService, DirectoryGraphService>();
builder.Services.AddSingleton<IMailboxContactGraphService, MailboxContactGraphService>();
builder.Services.AddSingleton<IContactMapper, ContactMapper>();
builder.Services.AddSingleton<ISyncRepository, SyncRepository>();
builder.Services.AddSingleton<ISyncOrchestrator, SyncOrchestrator>();
builder.Services.AddHostedService<Worker>();

builder.AddContactRelayLogging();

var host = builder.Build();
_ = host.Services.GetRequiredService<GraphServiceClient>();
await host.RunAsync();
