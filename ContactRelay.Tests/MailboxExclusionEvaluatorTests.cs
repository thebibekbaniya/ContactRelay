using ContactRelay.Graph;
using ContactRelay.Options;
using Microsoft.Graph.Models;

namespace ContactRelay.Tests;

public sealed class MailboxExclusionEvaluatorTests
{
    [Fact]
    public void IsExcluded_ReturnsTrue_ForDisabledUsers()
    {
        var user = new User { AccountEnabled = false, Mail = "person@example.test", UserType = "Member" };

        Assert.True(MailboxExclusionEvaluator.IsExcluded(user, Options()));
    }

    [Fact]
    public void IsExcluded_ReturnsTrue_WhenNoMailboxIdentifierExists()
    {
        var user = new User { AccountEnabled = true, UserType = "Member" };

        Assert.True(MailboxExclusionEvaluator.IsExcluded(user, Options()));
    }

    [Fact]
    public void IsExcluded_ReturnsTrue_ForSharedMailboxPurpose()
    {
        var user = new User { AccountEnabled = true, Mail = "shared@example.test", UserType = "Member" };

        Assert.True(MailboxExclusionEvaluator.IsExcluded(user, Options(), "shared"));
    }

    [Fact]
    public void IsExcluded_ReturnsTrue_ForServiceAccountPattern()
    {
        var user = new User
        {
            AccountEnabled = true,
            UserPrincipalName = "svc-directory@example.test",
            Mail = "svc-directory@example.test",
            UserType = "Member"
        };

        Assert.True(MailboxExclusionEvaluator.IsExcluded(user, Options()));
    }

    private static SyncWorkerOptions Options()
    {
        return new SyncWorkerOptions
        {
            ExcludeDisabledAccounts = true,
            ExcludeSharedMailboxes = true,
            ExcludeServiceAccounts = true,
            ExcludedAccountNamePatterns = ["svc", "service"]
        };
    }
}
