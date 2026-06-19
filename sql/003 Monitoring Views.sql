:setvar DatabaseName "ContactRelay"

USE [$(DatabaseName)]
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================================================
-- ContactRelay monitoring views. Re-runnable (CREATE OR ALTER); safe to apply
-- any time and in any order relative to the schema migrations. All timestamps
-- are UTC. Quick start (see examples at the bottom of this file):
--   SELECT * FROM dbo.vw_SyncDashboard;
--   SELECT * FROM dbo.vw_MailboxesNeedingAttention;
-- =============================================================================

-- One-row health summary: when it last ran, current status, headline counts,
-- and how many mailboxes currently need attention. Start here.
CREATE OR ALTER VIEW dbo.vw_SyncDashboard
AS
WITH last_run AS
(
    SELECT TOP (1) *
    FROM dbo.SyncRun
    ORDER BY SyncRunId DESC
)
SELECT
    lr.SyncRunId                                                              AS LastRunId,
    lr.Status                                                                 AS LastRunStatus,
    lr.IsDryRun                                                               AS LastRunWasDryRun,
    CASE WHEN lr.Status = 'Running' THEN 1 ELSE 0 END                         AS IsCurrentlyRunning,
    lr.StartedUtc                                                             AS LastRunStartedUtc,
    lr.CompletedUtc                                                           AS LastRunCompletedUtc,
    DATEDIFF(MINUTE, lr.StartedUtc, SYSUTCDATETIME())                         AS MinutesSinceLastStart,
    DATEDIFF(SECOND, lr.StartedUtc, ISNULL(lr.CompletedUtc, SYSUTCDATETIME())) AS LastRunDurationSeconds,
    lr.TargetMailboxCount,
    lr.PublishedContactCount,
    lr.CreatedCount,
    lr.UpdatedCount,
    lr.DeletedCount,
    lr.SkippedCount,
    lr.ErrorCount,
    lr.ErrorMessage                                                           AS LastRunErrorMessage,
    (SELECT COUNT(*) FROM dbo.MailboxFolderSyncState
        WHERE IsDeleted = 0 AND LastMailboxSyncStatus = 'Failed')             AS MailboxesFailed,
    (SELECT COUNT(*) FROM dbo.MailboxFolderSyncState
        WHERE IsDeleted = 0 AND LastMailboxSyncStatus = 'CompletedWithErrors') AS MailboxesWithContactErrors,
    (SELECT COUNT(*) FROM dbo.MailboxFolderSyncState
        WHERE IsDeleted = 0 AND LastMailboxSyncStatus = 'Skipped')            AS MailboxesSkippedNotRestEnabled,
    (SELECT COUNT(*) FROM dbo.MailboxFolderSyncState
        WHERE IsDeleted = 0 AND LegacyFolderCleanupStatus = 'Failed')         AS LegacyCleanupsFailed,
    (SELECT SettingValue FROM dbo.SyncSettings WHERE SettingKey = 'DryRun')   AS DryRunSetting
FROM last_run lr;
GO

-- Recent run history with duration and headline counts. ORDER BY in your query.
CREATE OR ALTER VIEW dbo.vw_SyncRunHistory
AS
SELECT TOP (200)
    r.SyncRunId,
    r.Status,
    r.IsDryRun,
    r.StartedUtc,
    r.CompletedUtc,
    DATEDIFF(SECOND, r.StartedUtc, ISNULL(r.CompletedUtc, SYSUTCDATETIME())) AS DurationSeconds,
    r.HostName,
    r.TargetMailboxCount,
    r.PublishedContactCount,
    r.CreatedCount,
    r.UpdatedCount,
    r.DeletedCount,
    r.SkippedCount,
    r.ErrorCount,
    r.ErrorMessage
FROM dbo.SyncRun r
ORDER BY r.SyncRunId DESC;
GO

-- Action/result breakdown for the latest run (Create/Update/Delete/Skip/Error x Success/Planned/Skipped/Failed).
CREATE OR ALTER VIEW dbo.vw_LatestRunBreakdown
AS
SELECT
    i.SyncRunId,
    i.Action,
    i.Result,
    COUNT(*) AS ItemCount
FROM dbo.SyncRunItem i
WHERE i.SyncRunId = (SELECT MAX(SyncRunId) FROM dbo.SyncRun)
GROUP BY i.SyncRunId, i.Action, i.Result;
GO

-- Every failure in the latest run: which mailbox, which contact, error code, message.
CREATE OR ALTER VIEW dbo.vw_LatestRunErrors
AS
SELECT
    i.SyncRunId,
    i.TargetMailboxUpn,
    i.SourceUserObjectId,
    i.Action,
    i.Result,
    i.ErrorCode,
    i.Message,
    i.CreatedUtc
FROM dbo.SyncRunItem i
WHERE i.SyncRunId = (SELECT MAX(SyncRunId) FROM dbo.SyncRun)
  AND i.Result = 'Failed';
GO

-- What was skipped in the latest run, grouped by reason with counts.
CREATE OR ALTER VIEW dbo.vw_LatestRunSkips
AS
SELECT
    i.SyncRunId,
    i.Action,
    i.ErrorCode,
    i.Message,
    COUNT(*) AS ItemCount
FROM dbo.SyncRunItem i
WHERE i.SyncRunId = (SELECT MAX(SyncRunId) FROM dbo.SyncRun)
  AND i.Result = 'Skipped'
GROUP BY i.SyncRunId, i.Action, i.ErrorCode, i.Message;
GO

-- Current per-mailbox state (managed folder + legacy cleanup).
CREATE OR ALTER VIEW dbo.vw_MailboxStatus
AS
SELECT
    s.TargetMailboxUpn,
    s.ManagedFolderName,
    s.LastMailboxSyncStatus,
    s.LastSuccessfulSyncUtc,
    s.LastFailedSyncUtc,
    s.LastErrorMessage,
    s.LegacyFolderName,
    s.LegacyFolderCleanupStatus,
    s.LegacyFolderCleanupAttemptedUtc,
    s.LegacyFolderCleanedUtc,
    s.UpdatedUtc
FROM dbo.MailboxFolderSyncState s
WHERE s.IsDeleted = 0;
GO

-- Actionable list: only mailboxes that need a human look, with a plain-English reason.
CREATE OR ALTER VIEW dbo.vw_MailboxesNeedingAttention
AS
SELECT
    s.TargetMailboxUpn,
    s.LastMailboxSyncStatus,
    s.LastFailedSyncUtc,
    s.LastErrorMessage,
    s.LegacyFolderCleanupStatus,
    CASE
        WHEN s.LastMailboxSyncStatus = 'Failed'
            THEN 'Mailbox sync failed - investigate LastErrorMessage and vw_LatestRunErrors.'
        WHEN s.LastMailboxSyncStatus = 'CompletedWithErrors'
            THEN 'Some contacts failed - review failed items for this mailbox in SyncRunItem.'
        WHEN s.LastMailboxSyncStatus = 'Skipped'
            THEN 'Not a Graph/REST mailbox (on-prem/hybrid, unlicensed, or no EXO mailbox). Exclude from scope if expected.'
        WHEN s.LegacyFolderCleanupStatus = 'Failed'
            THEN 'Legacy folder cleanup failed - review legacy folder for this mailbox.'
        ELSE 'Review.'
    END AS Reason
FROM dbo.MailboxFolderSyncState s
WHERE s.IsDeleted = 0
  AND (s.LastMailboxSyncStatus IN ('Failed', 'CompletedWithErrors', 'Skipped')
       OR s.LegacyFolderCleanupStatus = 'Failed');
GO

-- =============================================================================
-- Examples
--   SELECT * FROM dbo.vw_SyncDashboard;
--   SELECT * FROM dbo.vw_SyncRunHistory ORDER BY SyncRunId DESC;
--   SELECT * FROM dbo.vw_LatestRunBreakdown ORDER BY ItemCount DESC;
--   SELECT * FROM dbo.vw_LatestRunErrors;
--   SELECT * FROM dbo.vw_LatestRunSkips ORDER BY ItemCount DESC;
--   SELECT * FROM dbo.vw_MailboxesNeedingAttention;
--   SELECT * FROM dbo.vw_MailboxStatus WHERE TargetMailboxUpn = 'someone@contoso.com';
-- =============================================================================
