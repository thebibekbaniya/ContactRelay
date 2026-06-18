:setvar DatabaseName "ContactRelay"

USE [$(DatabaseName)]
GO

-- Allow the 'Emptied' legacy-folder cleanup status. This is recorded when Exchange
-- refuses to delete a legacy contact folder (ErrorCannotDeleteObject, e.g. a renamed
-- default Contacts folder) and ContactRelay falls back to removing its contacts and
-- leaving the folder in place. Safe to run repeatedly.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_MailboxFolderSyncState_LegacyFolderCleanupStatus')
BEGIN
    ALTER TABLE [dbo].[MailboxFolderSyncState] DROP CONSTRAINT [CK_MailboxFolderSyncState_LegacyFolderCleanupStatus];
END
GO

ALTER TABLE [dbo].[MailboxFolderSyncState]  WITH CHECK ADD  CONSTRAINT [CK_MailboxFolderSyncState_LegacyFolderCleanupStatus] CHECK  (([LegacyFolderCleanupStatus] IS NULL OR ([LegacyFolderCleanupStatus]='Failed' OR [LegacyFolderCleanupStatus]='Deleted' OR [LegacyFolderCleanupStatus]='Emptied' OR [LegacyFolderCleanupStatus]='SkippedDryRun' OR [LegacyFolderCleanupStatus]='NotFound')))
GO
ALTER TABLE [dbo].[MailboxFolderSyncState] CHECK CONSTRAINT [CK_MailboxFolderSyncState_LegacyFolderCleanupStatus]
GO

-- Allow the 'Skipped' mailbox sync status. This is recorded when a target cannot be synced
-- via Microsoft Graph/REST (MailboxNotEnabledForRESTAPI: on-premises/hybrid mailbox, an
-- unlicensed user, or a mail user/contact with no Exchange Online mailbox). Such targets are
-- skipped instead of counted as failures. Safe to run repeatedly.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_MailboxFolderSyncState_LastMailboxSyncStatus')
BEGIN
    ALTER TABLE [dbo].[MailboxFolderSyncState] DROP CONSTRAINT [CK_MailboxFolderSyncState_LastMailboxSyncStatus];
END
GO

ALTER TABLE [dbo].[MailboxFolderSyncState]  WITH CHECK ADD  CONSTRAINT [CK_MailboxFolderSyncState_LastMailboxSyncStatus] CHECK  (([LastMailboxSyncStatus]='Failed' OR [LastMailboxSyncStatus]='CompletedWithErrors' OR [LastMailboxSyncStatus]='Success' OR [LastMailboxSyncStatus]='Running' OR [LastMailboxSyncStatus]='Skipped' OR [LastMailboxSyncStatus]='Unknown'))
GO
ALTER TABLE [dbo].[MailboxFolderSyncState] CHECK CONSTRAINT [CK_MailboxFolderSyncState_LastMailboxSyncStatus]
GO