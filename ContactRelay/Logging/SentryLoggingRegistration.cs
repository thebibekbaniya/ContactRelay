using Microsoft.Extensions.Logging.EventLog;
using Sentry.Extensions.Logging;
using Serilog;

namespace ContactRelay.Logging;

public static class SentryLoggingRegistration
{
    public static void AddContactRelayLogging(this IHostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
        builder.Logging.AddConsole();

        AddRollingFile(builder);

        if (OperatingSystem.IsWindows())
        {
            builder.Logging.AddEventLog(new EventLogSettings
            {
                SourceName = builder.Configuration.GetValue<string>("Service:Name")
                    ?? throw new InvalidOperationException("Service:Name is required.")
            });
        }

        var sentrySection = builder.Configuration.GetSection("Sentry");
        var sentryDsn = sentrySection["Dsn"];
        if (string.IsNullOrWhiteSpace(sentryDsn))
        {
            return;
        }

        builder.Logging.AddSentry(options =>
        {
            sentrySection.Bind(options);
            options.Dsn = sentryDsn;
            options.MinimumBreadcrumbLevel = LogLevel.Information;
            options.MinimumEventLevel = LogLevel.Error;
            options.InitializeSdk = true;
            options.SendDefaultPii = false;
        });
    }

    // Adds a rolling daily file sink alongside the console/Event Log/Sentry providers, so
    // operational logs survive service restarts (the console is not captured when running as
    // a Windows Service). File logging is opt-in (Logging:File:Enabled). The configured folder
    // is tried first, then ProgramData; if neither is writable, file logging is skipped rather
    // than failing startup. Category levels are still governed by the Logging:LogLevel section;
    // the Serilog minimum is left open so it does not filter on top of that.
    private static void AddRollingFile(IHostApplicationBuilder builder)
    {
        var fileSection = builder.Configuration.GetSection("Logging:File");

        if (!fileSection.GetValue<bool>("Enabled"))
        {
            return;
        }

        var retainedFileCountLimit = fileSection.GetValue<int?>("RetainedFileCountLimit") ?? 31;
        var fileSizeLimitBytes = fileSection.GetValue<long?>("FileSizeLimitBytes") ?? 50L * 1024 * 1024;

        // Candidate log directories in priority order: the configured folder, then ProgramData.
        var candidateDirectories = new List<string>();

        var configuredDirectory = fileSection["Directory"];
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            candidateDirectories.Add(Path.IsPathRooted(configuredDirectory)
                ? configuredDirectory
                : Path.Combine(builder.Environment.ContentRootPath, configuredDirectory));
        }

        candidateDirectories.Add(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ContactRelay", "logs"));

        foreach (var directory in candidateDirectories)
        {
            if (!IsDirectoryWritable(directory))
            {
                continue;
            }

            var fileLogger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.File(
                    Path.Combine(directory, "contactrelay-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: retainedFileCountLimit,
                    fileSizeLimitBytes: fileSizeLimitBytes,
                    rollOnFileSizeLimit: true,
                    shared: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            builder.Logging.AddSerilog(fileLogger, dispose: true);
            return;
        }

        // No writable location: continue without a log file rather than crashing startup.
        Console.Error.WriteLine(
            "ContactRelay: file logging is enabled but no writable log directory was found; continuing without a log file.");
    }

    private static bool IsDirectoryWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);

            // Probe with a real write so permission problems are caught now, not lazily by the sink.
            var probePath = Path.Combine(directory, ".contactrelay-write-test");
            using var stream = new FileStream(
                probePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1, FileOptions.DeleteOnClose);
            stream.WriteByte(0);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
