using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Graph.Models;
using ContactRelay.Options;

namespace ContactRelay.Graph;

public static partial class MailboxExclusionEvaluator
{
    private static readonly ConcurrentDictionary<string, Regex> _regexCache = new(StringComparer.Ordinal);
    public static bool IsExcluded(User user, SyncWorkerOptions options, string? mailboxPurpose = null)
    {
        if (options.ExcludeDisabledAccounts && user.AccountEnabled != true)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(user.Mail) && string.IsNullOrWhiteSpace(user.UserPrincipalName))
        {
            return true;
        }

        if (!string.Equals(user.UserType, "Member", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(user.UserType))
        {
            return true;
        }

        if (IsExcludedMailboxPurpose(mailboxPurpose, options))
        {
            return true;
        }

        return IsExcludedByPattern(user, options);
    }

    public static bool IsExcludedMailboxPurpose(string? mailboxPurpose, SyncWorkerOptions options)
    {
        var normalized = NormalizePurpose(mailboxPurpose);
        return normalized switch
        {
            "shared" when options.ExcludeSharedMailboxes => true,
            "room" or "equipment" or "linkedroom" or "linked" when options.ExcludeRoomAndResourceMailboxes => true,
            "community" when options.ExcludeCommunityMailboxes => true,
            _ => false
        };
    }

    private static bool IsExcludedByPattern(User user, SyncWorkerOptions options)
    {
        var identityParts = new[]
        {
            user.UserPrincipalName,
            user.Mail,
            user.DisplayName
        };

        foreach (var pattern in options.ExcludedAccountNamePatterns.Where(pattern => !string.IsNullOrWhiteSpace(pattern)))
        {
            var trimmedPattern = pattern.Trim();
            if (!IsPatternEnabled(trimmedPattern, options))
            {
                continue;
            }

            var normalizedPattern = Regex.Escape(trimmedPattern);
            var boundaryPattern = $@"(^|[^\p{{L}}\p{{N}}]){normalizedPattern}([^\p{{L}}\p{{N}}]|$)";

            var regex = _regexCache.GetOrAdd(boundaryPattern, p => WordBoundaryRegex(p));
            if (identityParts.Any(part => part is not null && regex.IsMatch(part)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPatternEnabled(string pattern, SyncWorkerOptions options)
    {
        return pattern.ToLowerInvariant() switch
        {
            "svc" or "service" => options.ExcludeServiceAccounts,
            "community" => options.ExcludeCommunityMailboxes,
            "shared" => options.ExcludeSharedMailboxes,
            "room" or "resource" or "equipment" => options.ExcludeRoomAndResourceMailboxes,
            _ => true
        };
    }

    private static string? NormalizePurpose(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal).Trim().ToLowerInvariant();
    }

    private static Regex WordBoundaryRegex(string pattern)
    {
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }
}
