using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using ContactRelay.Mapping;
using ContactRelay.Models;
using ContactRelay.Options;
using ContactRelay.Utilities;

namespace ContactRelay.Graph;

public sealed class DirectoryGraphService(
    GraphServiceClient graph,
    IGraphRetryHandler retryHandler,
    IContactMapper mapper,
    IOptions<GraphOptions> graphOptions,
    IOptions<SyncWorkerOptions> syncOptions,
    ILogger<DirectoryGraphService> logger) : IDirectoryGraphService
{
    private readonly GraphOptions _graphOptions = graphOptions.Value;
    private readonly SyncWorkerOptions _syncOptions = syncOptions.Value;
    private readonly Dictionary<string, string?> _mailboxPurposeByUserId = new(StringComparer.OrdinalIgnoreCase);
    private bool _mailboxPurposeLookupDenied;
    private IReadOnlyList<User>? _allEnabledMailUsers;

    public void ClearRunCache()
    {
        _allEnabledMailUsers = null;
        _mailboxPurposeByUserId.Clear();
        _mailboxPurposeLookupDenied = false;
    }

    public async Task<IReadOnlyList<DirectoryContact>> GetPublishedContactsAsync(ISet<Guid> includedSourceIds, CancellationToken cancellationToken)
    {
        var groupFilterIds = await GetGroupMemberIdsAsync(_graphOptions.PublishedContactSecurityGroupId, cancellationToken);
        var useGroupFilter = groupFilterIds.Count > 0;
        var useSqlInclusionFilter = includedSourceIds.Count > 0;

        var users = useGroupFilter
            ? await GetUsersByIdsAsync(groupFilterIds.Union(includedSourceIds), cancellationToken)
            : await GetAllEnabledMailUsersAsync(cancellationToken);

        if (!useGroupFilter && useSqlInclusionFilter)
        {
            var includedUsers = await GetUsersByIdsAsync(includedSourceIds, cancellationToken);
            users = users
                .Concat(includedUsers)
                .GroupBy(user => user.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
        }

        var contacts = new List<DirectoryContact>(users.Count);

        foreach (var user in users)
        {
            if (!Guid.TryParse(user.Id, out var userId))
            {
                continue;
            }

            if (useGroupFilter && !groupFilterIds.Contains(userId) && !includedSourceIds.Contains(userId))
            {
                continue;
            }

            if (await IsExcludedByDefaultAsync(user, cancellationToken))
            {
                continue;
            }

            var manager = GetExpandedManagerDisplayName(user);
            contacts.Add(mapper.MapUser(user, manager, _syncOptions));
        }

        return contacts;
    }

    public async Task<IReadOnlyList<TargetMailbox>> GetTargetMailboxesFromGroupAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_graphOptions.TargetMailboxSecurityGroupId))
        {
            return [];
        }

        var members = await GetGroupUsersAsync(_graphOptions.TargetMailboxSecurityGroupId, cancellationToken);
        return await ToTargetMailboxesAsync(members, "GraphGroup", cancellationToken);
    }

    public async Task<IReadOnlyList<TargetMailbox>> GetAllUserMailboxesAsync(CancellationToken cancellationToken)
    {
        var users = await GetAllEnabledMailUsersAsync(cancellationToken);
        var mailboxes = await ToTargetMailboxesAsync(users, "GraphAllUsers", cancellationToken);

        logger.LogInformation("Selected {Count} enabled user mailboxes from Microsoft Graph.", mailboxes.Count);
        return mailboxes;
    }

    private async Task<IReadOnlyList<User>> GetAllEnabledMailUsersAsync(CancellationToken cancellationToken)
    {
        if (_allEnabledMailUsers is not null)
        {
            logger.LogDebug("Using cached enabled mail users from Microsoft Graph.");
            return _allEnabledMailUsers;
        }

        var users = new List<User>();
        var page = await retryHandler.ExecuteAsync(
            ct => graph.Users.GetAsync(request =>
            {
                request.QueryParameters.Top = _graphOptions.PageSize;
                request.QueryParameters.Filter = "accountEnabled eq true and userType eq 'Member'";
                request.QueryParameters.Select =
                [
                    "id",
                    "userPrincipalName",
                    "mail",
                    "givenName",
                    "surname",
                    "displayName",
                    "jobTitle",
                    "department",
                    "mobilePhone",
                    "businessPhones",
                    "accountEnabled",
                    "userType",
                    "employeeId"
                ];
                request.QueryParameters.Expand = ["manager($select=id,displayName,userPrincipalName,mail)"];
            }, ct),
            cancellationToken);

        while (page is not null)
        {
            if (page.Value is not null)
            {
                users.AddRange(page.Value.Where(user => !string.IsNullOrWhiteSpace(user.Mail)));
            }

            if (string.IsNullOrWhiteSpace(page.OdataNextLink))
            {
                break;
            }

            var nextLink = page.OdataNextLink;
            page = await retryHandler.ExecuteAsync(
                ct => graph.Users.WithUrl(nextLink).GetAsync(cancellationToken: ct),
                cancellationToken);
        }

        logger.LogInformation("Loaded {Count} enabled mail users from Microsoft Graph.", users.Count);
        _allEnabledMailUsers = users;
        return _allEnabledMailUsers;
    }

    private async Task<IReadOnlySet<Guid>> GetGroupMemberIdsAsync(string? groupId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return new HashSet<Guid>();
        }

        var users = await GetGroupUsersAsync(groupId, cancellationToken);
        return users
            .Select(user => Guid.TryParse(user.Id, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();
    }

    private async Task<IReadOnlyList<User>> GetGroupUsersAsync(string groupId, CancellationToken cancellationToken)
    {
        var users = new List<User>();
        var page = await retryHandler.ExecuteAsync(
            ct => graph.Groups[groupId].TransitiveMembers.GetAsync(request =>
            {
                request.QueryParameters.Top = _graphOptions.PageSize;
                request.QueryParameters.Count = true;
                request.QueryParameters.Select =
                [
                    "id",
                    "userPrincipalName",
                    "mail",
                    "givenName",
                    "surname",
                    "displayName",
                    "jobTitle",
                    "department",
                    "mobilePhone",
                    "businessPhones",
                    "accountEnabled",
                    "userType",
                    "employeeId"
                ];
                request.Headers.Add("ConsistencyLevel", "eventual");
            }, ct),
            cancellationToken);

        while (page is not null)
        {
            if (page.Value is not null)
            {
                users.AddRange(page.Value.OfType<User>());
            }

            if (string.IsNullOrWhiteSpace(page.OdataNextLink))
            {
                break;
            }

            var nextLink = page.OdataNextLink;
            page = await retryHandler.ExecuteAsync(
                ct => graph.Groups[groupId].TransitiveMembers.WithUrl(nextLink).GetAsync(cancellationToken: ct),
                cancellationToken);
        }

        logger.LogInformation(
            "Loaded {Count} users from configured Graph group {GroupId}.",
            users.Count,
            LogRedactor.Identifier(groupId, _syncOptions));
        return users;
    }

    private async Task<IReadOnlyList<User>> GetUsersByIdsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var users = new System.Collections.Concurrent.ConcurrentBag<User>();

        await Parallel.ForEachAsync(
            userIds.Distinct(),
            new ParallelOptions { MaxDegreeOfParallelism = _graphOptions.UserLookupConcurrency, CancellationToken = cancellationToken },
            async (userId, ct) =>
            {
                try
                {
                    var user = await retryHandler.ExecuteAsync(
                        innerCt => graph.Users[userId.ToString()].GetAsync(request =>
                        {
                            request.QueryParameters.Select =
                            [
                                "id",
                                "userPrincipalName",
                                "mail",
                                "givenName",
                                "surname",
                                "displayName",
                                "jobTitle",
                                "department",
                                "mobilePhone",
                                "businessPhones",
                                "accountEnabled",
                                "userType",
                                "employeeId",
                                "mailboxSettings"
                            ];
                            request.QueryParameters.Expand = ["manager($select=id,displayName,userPrincipalName,mail)"];
                        }, innerCt),
                        ct);

                    if (user is not null)
                        users.Add(user);
                }
                catch (Exception ex)
                {
                    var graphError = GraphExceptionDetails.From(ex);
                    logger.LogWarning(
                        "Unable to load Graph user {UserId}; continuing. ErrorType={ErrorType} GraphError={GraphError}",
                        LogRedactor.Identifier(userId, _syncOptions),
                        ex.GetType().Name,
                        graphError.ToSummary());
                }
            });

        return [.. users];
    }

    private async Task<IReadOnlyList<TargetMailbox>> ToTargetMailboxesAsync(
        IReadOnlyList<User> users,
        string source,
        CancellationToken cancellationToken)
    {
        var mailboxes = new List<TargetMailbox>(users.Count);
        foreach (var user in users)
        {
            if (await IsExcludedByDefaultAsync(user, cancellationToken))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(user.Mail) && string.IsNullOrWhiteSpace(user.UserPrincipalName))
            {
                continue;
            }

            mailboxes.Add(new TargetMailbox
            {
                EntraUserObjectId = Guid.TryParse(user.Id, out var id) ? id : null,
                UserPrincipalName = user.UserPrincipalName ?? user.Mail!,
                Mail = user.Mail ?? user.UserPrincipalName!,
                DisplayName = user.DisplayName,
                IsEnabled = true,
                Source = source
            });
        }

        return mailboxes;
    }

    private async Task<bool> IsExcludedByDefaultAsync(User user, CancellationToken cancellationToken)
    {
        var mailboxPurpose = user.MailboxSettings?.UserPurpose?.ToString();
        if (string.IsNullOrWhiteSpace(mailboxPurpose) && !string.IsNullOrWhiteSpace(user.Id))
        {
            if (!_mailboxPurposeByUserId.TryGetValue(user.Id, out mailboxPurpose))
            {
                mailboxPurpose = await GetMailboxPurposeAsync(user.Id, cancellationToken);
                _mailboxPurposeByUserId[user.Id] = mailboxPurpose;
            }
        }

        return MailboxExclusionEvaluator.IsExcluded(user, _syncOptions, mailboxPurpose);
    }

    private async Task<string?> GetMailboxPurposeAsync(string userId, CancellationToken cancellationToken)
    {
        if (_mailboxPurposeLookupDenied)
        {
            return null;
        }

        if (!_syncOptions.ExcludeSharedMailboxes
            && !_syncOptions.ExcludeRoomAndResourceMailboxes
            && !_syncOptions.ExcludeCommunityMailboxes)
        {
            return null;
        }

        try
        {
            var settings = await retryHandler.ExecuteAsync(
                ct => graph.Users[userId].MailboxSettings.GetAsync(cancellationToken: ct),
                cancellationToken);
            return settings?.UserPurpose?.ToString();
        }
        catch (Exception ex)
        {
            var graphError = GraphExceptionDetails.From(ex);
            if (graphError.StatusCode == 403)
            {
                _mailboxPurposeLookupDenied = true;
                logger.LogWarning(
                    "Microsoft Graph denied mailboxSettings.userPurpose lookup. Falling back to configured name patterns for this sync run. GraphError={GraphError}",
                    graphError.ToSummary());
                return null;
            }

            if (graphError.StatusCode == 404 &&
                string.Equals(graphError.Code, "MailboxNotEnabledForRESTAPI", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogDebug(
                    "Microsoft Graph cannot read mailboxSettings.userPurpose for user {UserId} because the mailbox is inactive, soft-deleted, or hosted on-premise. Falling back to configured name patterns. GraphError={GraphError}",
                    LogRedactor.Identifier(userId, _syncOptions),
                    graphError.ToSummary());
                return null;
            }

            logger.LogWarning(
                "Unable to read mailboxSettings.userPurpose for Graph user {UserId}; falling back to configured name patterns. ErrorType={ErrorType} GraphError={GraphError}",
                LogRedactor.Identifier(userId, _syncOptions),
                ex.GetType().Name,
                graphError.ToSummary());
            return null;
        }
    }

    private static string? GetExpandedManagerDisplayName(User user)
    {
        return user.Manager switch
        {
            User manager => manager.DisplayName ?? manager.UserPrincipalName ?? manager.Mail,
            OrgContact contact => contact.DisplayName,
            _ => null
        };
    }
}
