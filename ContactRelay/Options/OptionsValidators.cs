using Microsoft.Extensions.Options;

namespace ContactRelay.Options;

public sealed class ServiceOptionsValidator : IValidateOptions<ServiceOptions>
{
    public ValidateOptionsResult Validate(string? name, ServiceOptions options)
    {
        return string.IsNullOrWhiteSpace(options.Name) || ConfigurationPlaceholder.IsPlaceholder(options.Name)
            ? ValidateOptionsResult.Fail("Service:Name is required and must not contain a placeholder value.")
            : ValidateOptionsResult.Success;
    }
}

public sealed class GraphOptionsValidator : IValidateOptions<GraphOptions>
{
    public ValidateOptionsResult Validate(string? name, GraphOptions options)
    {
        var failures = new List<string>();

        RequireConfigured(options.TenantId, "Graph:TenantId", failures);
        RequireConfigured(options.ClientId, "Graph:ClientId", failures);

        if (IsPlaceholder(options.ClientSecret))
        {
            failures.Add("Graph:ClientSecret contains a placeholder value.");
        }

        if (IsPlaceholder(options.CertificateThumbprint))
        {
            failures.Add("Graph:CertificateThumbprint contains a placeholder value.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientSecret) && string.IsNullOrWhiteSpace(options.CertificateThumbprint))
        {
            failures.Add("Graph authentication requires either Graph:ClientSecret or Graph:CertificateThumbprint.");
        }

        if (options.PageSize is < 1 or > 999)
        {
            failures.Add("Graph:PageSize must be between 1 and 999.");
        }

        if (options.BatchSize is < 1 or > 20)
        {
            failures.Add("Graph:BatchSize must be between 1 and 20.");
        }

        if (options.MaxRetryAttempts < 1)
        {
            failures.Add("Graph:MaxRetryAttempts must be at least 1.");
        }

        if (options.BaseRetryDelaySeconds < 1)
        {
            failures.Add("Graph:BaseRetryDelaySeconds must be at least 1.");
        }

        if (options.UserLookupConcurrency is < 1 or > 64)
        {
            failures.Add("Graph:UserLookupConcurrency must be between 1 and 64.");
        }

        if (options.Scopes.Length == 0 || options.Scopes.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add("Graph:Scopes must contain at least one non-empty scope.");
        }

        if (!string.Equals(options.CertificateStoreLocation, "LocalMachine", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(options.CertificateStoreLocation, "CurrentUser", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("Graph:CertificateStoreLocation must be LocalMachine or CurrentUser.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void RequireConfigured(string? value, string key, ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{key} is required.");
            return;
        }

        if (IsPlaceholder(value))
        {
            failures.Add($"{key} contains a placeholder value.");
        }
    }

    private static bool IsPlaceholder(string? value)
    {
        return ConfigurationPlaceholder.IsPlaceholder(value);
    }
}

public sealed class SqlOptionsValidator : IValidateOptions<SqlOptions>
{
    public ValidateOptionsResult Validate(string? name, SqlOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail("Sql:ConnectionString is required.");
        }

        if (ConfigurationPlaceholder.IsPlaceholder(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail("Sql:ConnectionString contains a placeholder value.");
        }

        return options.CommandTimeoutSeconds > 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Sql:CommandTimeoutSeconds must be greater than 0.");
    }
}

public sealed class SyncWorkerOptionsValidator : IValidateOptions<SyncWorkerOptions>
{
    public ValidateOptionsResult Validate(string? name, SyncWorkerOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ManagedFolderName))
        {
            failures.Add("Sync:ManagedFolderName is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ManagedCategory))
        {
            failures.Add("Sync:ManagedCategory is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ManagedByMarker))
        {
            failures.Add("Sync:ManagedByMarker is required.");
        }

        if (ConfigurationPlaceholder.IsPlaceholder(options.ManagedFolderName)
            || ConfigurationPlaceholder.IsPlaceholder(options.ManagedCategory)
            || ConfigurationPlaceholder.IsPlaceholder(options.ManagedByMarker))
        {
            failures.Add("Sync managed folder, category, and marker values must not contain placeholders.");
        }

        if (string.Equals(options.ManagedFolderName, options.LegacyContactFolderName, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("Sync:ManagedFolderName must not match Sync:LegacyContactFolderName.");
        }

        if (options.LegacyManagedFolderNames.Any(folderName => string.Equals(options.ManagedFolderName, folderName, StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add("Sync:ManagedFolderName must not appear in Sync:LegacyManagedFolderNames.");
        }

        if (!SyncSchedule.TryGetDailyRunTime(options, out _) && options.IntervalMinutes < 1)
        {
            failures.Add("Sync:Schedule or Sync:DailyRunTime must be a valid daily local run time, or Sync:IntervalMinutes must be at least 1.");
        }

        if (options.MailboxConcurrency is < 1 or > 64)
        {
            failures.Add("Sync:MailboxConcurrency must be between 1 and 64.");
        }

        if (options.StaleRunTimeoutHours < 1)
        {
            failures.Add("Sync:StaleRunTimeoutHours must be at least 1.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

internal static class ConfigurationPlaceholder
{
    public static bool IsPlaceholder(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && (value.Contains("TODO", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("<", StringComparison.Ordinal)
                   || value.Contains(">", StringComparison.Ordinal));
    }
}
