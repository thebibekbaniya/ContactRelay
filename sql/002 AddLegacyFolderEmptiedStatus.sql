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
