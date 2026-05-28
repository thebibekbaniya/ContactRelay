using Microsoft.Extensions.Logging.EventLog;
using Sentry.Extensions.Logging;

namespace ContactRelay.Logging;

public static class SentryLoggingRegistration
{
    public static void AddContactRelayLogging(this IHostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
        builder.Logging.AddConsole();

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
}
