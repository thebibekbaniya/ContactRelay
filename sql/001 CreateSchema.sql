:setvar DatabaseName "ContactRelay"

USE [master]
GO


IF DB_ID(N'$(DatabaseName)') IS NULL
BEGIN
    EXEC(N'CREATE DATABASE [$(DatabaseName)]');
END
GO

USE [$(DatabaseName)]
GO

/****** Object:  Table [dbo].[ContactSyncState]    Script Date: 2026-05-28 12:42:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ContactSyncState](
	[ContactSyncStateId] [bigint] IDENTITY(1,1) NOT NULL,
	[TargetMailboxId] [bigint] NULL,
	[TargetMailboxUpn] [nvarchar](320) NOT NULL,
	[SourceUserObjectId] [uniqueidentifier] NOT NULL,
	[ExchangeContactId] [nvarchar](512) NULL,
	[LastFieldHash] [char](64) NULL,
	[IsDeleted] [bit] NOT NULL,
	[LastSyncedUtc] [datetime2](3) NULL,
	[DeletedUtc] [datetime2](3) NULL,
	[CreatedUtc] [datetime2](3) NOT NULL,
	[UpdatedUtc] [datetime2](3) NOT NULL,
	[RowVersion] [timestamp] NOT NULL,
 CONSTRAINT [PK_ContactSyncState] PRIMARY KEY CLUSTERED 
(
	[ContactSyncStateId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_ContactSyncState_Target_Source] UNIQUE NONCLUSTERED 
(
	[TargetMailboxUpn] ASC,
	[SourceUserObjectId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[FieldMapping]    Script Date: 2026-05-28 12:42:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[FieldMapping](
	[FieldMappingId] [int] IDENTITY(1,1) NOT NULL,
	[SourceField] [nvarchar](128) NOT NULL,
	[TargetField] [nvarchar](128) NOT NULL,
	[TransformName] [nvarchar](128) NULL,
	[IsEnabled] [bit] NOT NULL,
	[CreatedUtc] [datetime2](3) NOT NULL,
	[UpdatedUtc] [datetime2](3) NOT NULL,
	[RowVersion] [timestamp] NOT NULL,
 CONSTRAINT [PK_FieldMapping] PRIMARY KEY CLUSTERED 
(
	[FieldMappingId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_FieldMapping_Source_Target] UNIQUE NONCLUSTERED 
(
	[SourceField] ASC,
	[TargetField] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MailboxFolderSyncState]    Script Date: 2026-05-28 12:42:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MailboxFolderSyncState](
	[MailboxFolderSyncStateId] [bigint] IDENTITY(1,1) NOT NULL,
	[TargetMailboxUpn] [nvarchar](320) NOT NULL,
	[ManagedFolderName] [nvarchar](256) NOT NULL,
	[ManagedFolderId] [nvarchar](512) NULL,
	[LastMailboxSyncStatus] [nvarchar](32) NOT NULL,
	[LastSuccessfulSyncUtc] [datetime2](3) NULL,
	[LastFailedSyncUtc] [datetime2](3) NULL,
	[LastErrorMessage] [nvarchar](4000) NULL,
	[LegacyFolderName] [nvarchar](256) NULL,
	[LegacyFolderId] [nvarchar](512) NULL,
	[LegacyFolderCleanupStatus] [nvarchar](32) NULL,
	[LegacyFolderCleanupAttemptedUtc] [datetime2](3) NULL,
	[LegacyFolderCleanedUtc] [datetime2](3) NULL,
	[IsEnabled] [bit] NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[CreatedUtc] [datetime2](3) NOT NULL,
	[UpdatedUtc] [datetime2](3) NOT NULL,
	[RowVersion] [timestamp] NOT NULL,
 CONSTRAINT [PK_MailboxFolderSyncState] PRIMARY KEY CLUSTERED 
(
	[MailboxFolderSyncStateId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_MailboxFolderSyncState_TargetMailboxUpn] UNIQUE NONCLUSTERED 
(
	[TargetMailboxUpn] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PublishedContact]    Script Date: 2026-05-28 12:42:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PublishedContact](
	[PublishedContactId] [bigint] IDENTITY(1,1) NOT NULL,
	[SourceUserObjectId] [uniqueidentifier] NOT NULL,
	[UserPrincipalName] [nvarchar](320) NULL,
	[Mail] [nvarchar](320) NOT NULL,
	[DisplayName] [nvarchar](256) NULL,
	[FirstName] [nvarchar](128) NULL,
	[LastName] [nvarchar](128) NULL,
	[JobTitle] [nvarchar](256) NULL,
	[Department] [nvarchar](256) NULL,
	[CompanyName] [nvarchar](128) NOT NULL,
	[MobilePhone] [nvarchar](64) NULL,
	[DeskPhone] [nvarchar](64) NULL,
	[Manager] [nvarchar](256) NULL,
	[EmployeeNumber] [nvarchar](64) NULL,
	[FieldHash] [char](64) NOT NULL,
	[FilterSource] [nvarchar](32) NOT NULL,
	[IsEnabled] [bit] NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[IsManualOverride] [bit] NOT NULL,
	[LastSeenUtc] [datetime2](3) NULL,
	[CreatedUtc] [datetime2](3) NOT NULL,
	[UpdatedUtc] [datetime2](3) NOT NULL,
	[RowVersion] [timestamp] NOT NULL,
 CONSTRAINT [PK_PublishedContact] PRIMARY KEY CLUSTERED 
(
	[PublishedContactId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_PublishedContact_SourceUserObjectId] UNIQUE NONCLUSTERED 
(
	[SourceUserObjectId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SyncExclusion]    Script Date: 2026-05-28 12:42:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SyncExclusion](
	[SyncExclusionId] [bigint] IDENTITY(1,1) NOT NULL,
	[ExclusionType] [nvarchar](64) NOT NULL,
	[ExclusionValue] [nvarchar](320) NOT NULL,
	[Reason] [nvarchar](512) NULL,
	[IsEnabled] [bit] NOT NULL,
	[CreatedUtc] [datetime2](3) NOT NULL,
	[UpdatedUtc] [datetime2](3) NOT NULL,
	[RowVersion] [timestamp] NOT NULL,
 CONSTRAINT [PK_SyncExclusion] PRIMARY KEY CLUSTERED 
(
	[SyncExclusionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_SyncExclusion_Type_Value] UNIQUE NONCLUSTERED 
(
	[ExclusionType] ASC,
	[ExclusionValue] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SyncRun]    Script Date: 2026-05-28 12:42:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SyncRun](
	[SyncRunId] [bigint] IDENTITY(1,1) NOT NULL,
	[StartedUtc] [datetime2](3) NOT NULL,
	[CompletedUtc] [datetime2](3) NULL,
	[Status] [nvarchar](32) NOT NULL,
	[IsDryRun] [bit] NOT NULL,
	[HostName] [nvarchar](128) NULL,
	[ProcessId] [int] NULL,
	[TargetMailboxCount] [int] NOT NULL,
	[PublishedContactCount] [int] NOT NULL,
	[CreatedCount] [int] NOT NULL,
	[UpdatedCount] [int] NOT NULL,
	[DeletedCount] [int] NOT NULL,
	[SkippedCount] [int] NOT NULL,
	[ErrorCount] [int] NOT NULL,
	[ErrorMessage] [nvarchar](4000) NULL,
	[CreatedUtc] [datetime2](3) NOT NULL,
	[UpdatedUtc] [datetime2](3) NOT NULL,
	[RowVersion] [timestamp] NOT NULL,
 CONSTRAINT [PK_SyncRun] PRIMARY KEY CLUSTERED 
(
	[SyncRunId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SyncRunItem]    Script Date: 2026-05-28 12:42:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SyncRunItem](
	[SyncRunItemId] [bigint] IDENTITY(1,1) NOT NULL,
	[SyncRunId] [bigint] NOT NULL,
	[TargetMailboxUpn] [nvarchar](320) NULL,
	[SourceUserObjectId] [uniqueidentifier] NULL,
	[Action] [nvarchar](32) NOT NULL,
	[Result] [nvarchar](32) NOT NULL,
	[GraphContactId] [nvarchar](512) NULL,
	[ErrorCode] [nvarchar](128) NULL,
	[Message] [nvarchar](4000) NULL,
	[CreatedUtc] [datetime2](3) NOT NULL,
 CONSTRAINT [PK_SyncRunItem] PRIMARY KEY CLUSTERED 
(
	[SyncRunItemId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SyncSettings]    Script Date: 2026-05-28 12:42:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SyncSettings](
	[SyncSettingId] [int] IDENTITY(1,1) NOT NULL,
	[SettingKey] [nvarchar](128) NOT NULL,
	[SettingValue] [nvarchar](2048) NULL,
	[ValueType] [nvarchar](32) NOT NULL,
	[Description] [nvarchar](512) NULL,
	[IsEnabled] [bit] NOT NULL,
	[CreatedUtc] [datetime2](3) NOT NULL,
	[UpdatedUtc] [datetime2](3) NOT NULL,
	[RowVersion] [timestamp] NOT NULL,
 CONSTRAINT [PK_SyncSettings] PRIMARY KEY CLUSTERED 
(
	[SyncSettingId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_SyncSettings_SettingKey] UNIQUE NONCLUSTERED 
(
	[SettingKey] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TargetMailbox]    Script Date: 2026-05-28 12:42:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TargetMailbox](
	[TargetMailboxId] [bigint] IDENTITY(1,1) NOT NULL,
	[EntraUserObjectId] [uniqueidentifier] NULL,
	[UserPrincipalName] [nvarchar](320) NOT NULL,
	[Mail] [nvarchar](320) NOT NULL,
	[DisplayName] [nvarchar](256) NULL,
	[FilterSource] [nvarchar](32) NOT NULL,
	[IsEnabled] [bit] NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[CreatedUtc] [datetime2](3) NOT NULL,
	[UpdatedUtc] [datetime2](3) NOT NULL,
	[RowVersion] [timestamp] NOT NULL,
 CONSTRAINT [PK_TargetMailbox] PRIMARY KEY CLUSTERED 
(
	[TargetMailboxId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_TargetMailbox_Mail] UNIQUE NONCLUSTERED 
(
	[Mail] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_TargetMailbox_UserPrincipalName] UNIQUE NONCLUSTERED 
(
	[UserPrincipalName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Index [IX_ContactSyncState_SourceUserObjectId]    Script Date: 2026-05-28 12:42:04 PM ******/
CREATE NONCLUSTERED INDEX [IX_ContactSyncState_SourceUserObjectId] ON [dbo].[ContactSyncState]
(
	[SourceUserObjectId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_ContactSyncState_TargetMailboxUpn]    Script Date: 2026-05-28 12:42:04 PM ******/
CREATE NONCLUSTERED INDEX [IX_ContactSyncState_TargetMailboxUpn] ON [dbo].[ContactSyncState]
(
	[TargetMailboxUpn] ASC,
	[IsDeleted] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_MailboxFolderSyncState_LegacyCleanup]    Script Date: 2026-05-28 12:42:04 PM ******/
CREATE NONCLUSTERED INDEX [IX_MailboxFolderSyncState_LegacyCleanup] ON [dbo].[MailboxFolderSyncState]
(
	[LegacyFolderCleanupStatus] ASC,
	[LegacyFolderCleanupAttemptedUtc] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_MailboxFolderSyncState_Status]    Script Date: 2026-05-28 12:42:04 PM ******/
CREATE NONCLUSTERED INDEX [IX_MailboxFolderSyncState_Status] ON [dbo].[MailboxFolderSyncState]
(
	[LastMailboxSyncStatus] ASC,
	[UpdatedUtc] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_PublishedContact_Enabled]    Script Date: 2026-05-28 12:42:04 PM ******/
CREATE NONCLUSTERED INDEX [IX_PublishedContact_Enabled] ON [dbo].[PublishedContact]
(
	[IsEnabled] ASC,
	[IsDeleted] ASC
)
INCLUDE([SourceUserObjectId],[Mail],[FieldHash]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_PublishedContact_Mail]    Script Date: 2026-05-28 12:42:04 PM ******/
CREATE NONCLUSTERED INDEX [IX_PublishedContact_Mail] ON [dbo].[PublishedContact]
(
	[Mail] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_SyncExclusion_Enabled]    Script Date: 2026-05-28 12:42:04 PM ******/
CREATE NONCLUSTERED INDEX [IX_SyncExclusion_Enabled] ON [dbo].[SyncExclusion]
(
	[IsEnabled] ASC,
	[ExclusionType] ASC
)
INCLUDE([ExclusionValue]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_SyncRun_StartedUtc]    Script Date: 2026-05-28 12:42:04 PM ******/
CREATE NONCLUSTERED INDEX [IX_SyncRun_StartedUtc] ON [dbo].[SyncRun]
(
	[StartedUtc] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_SyncRun_Status]    Script Date: 2026-05-28 12:42:04 PM ******/
CREATE NONCLUSTERED INDEX [IX_SyncRun_Status] ON [dbo].[SyncRun]
(
	[Status] ASC,
	[StartedUtc] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_SyncRunItem_Errors]    Script Date: 2026-05-28 12:42:04 PM ******/
CREATE NONCLUSTERED INDEX [IX_SyncRunItem_Errors] ON [dbo].[SyncRunItem]
(
	[Result] ASC,
	[CreatedUtc] DESC
)
WHERE ([Result]='Failed')
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_SyncRunItem_SyncRun]    Script Date: 2026-05-28 12:42:04 PM ******/
CREATE NONCLUSTERED INDEX [IX_SyncRunItem_SyncRun] ON [dbo].[SyncRunItem]
(
	[SyncRunId] ASC,
	[Result] ASC,
	[Action] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_SyncRunItem_Target_Source]    Script Date: 2026-05-28 12:42:04 PM ******/
CREATE NONCLUSTERED INDEX [IX_SyncRunItem_Target_Source] ON [dbo].[SyncRunItem]
(
	[TargetMailboxUpn] ASC,
	[SourceUserObjectId] ASC,
	[CreatedUtc] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_TargetMailbox_Enabled]    Script Date: 2026-05-28 12:42:04 PM ******/
CREATE NONCLUSTERED INDEX [IX_TargetMailbox_Enabled] ON [dbo].[TargetMailbox]
(
	[IsEnabled] ASC,
	[IsDeleted] ASC
)
INCLUDE([Mail],[UserPrincipalName]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_TargetMailbox_EntraUserObjectId]    Script Date: 2026-05-28 12:42:04 PM ******/
CREATE NONCLUSTERED INDEX [IX_TargetMailbox_EntraUserObjectId] ON [dbo].[TargetMailbox]
(
	[EntraUserObjectId] ASC
)
WHERE ([EntraUserObjectId] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[ContactSyncState] ADD  CONSTRAINT [DF_ContactSyncState_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[ContactSyncState] ADD  CONSTRAINT [DF_ContactSyncState_CreatedUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedUtc]
GO
ALTER TABLE [dbo].[ContactSyncState] ADD  CONSTRAINT [DF_ContactSyncState_UpdatedUtc]  DEFAULT (sysutcdatetime()) FOR [UpdatedUtc]
GO
ALTER TABLE [dbo].[FieldMapping] ADD  CONSTRAINT [DF_FieldMapping_IsEnabled]  DEFAULT ((1)) FOR [IsEnabled]
GO
ALTER TABLE [dbo].[FieldMapping] ADD  CONSTRAINT [DF_FieldMapping_CreatedUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedUtc]
GO
ALTER TABLE [dbo].[FieldMapping] ADD  CONSTRAINT [DF_FieldMapping_UpdatedUtc]  DEFAULT (sysutcdatetime()) FOR [UpdatedUtc]
GO
ALTER TABLE [dbo].[MailboxFolderSyncState] ADD  CONSTRAINT [DF_MailboxFolderSyncState_LastMailboxSyncStatus]  DEFAULT ('Unknown') FOR [LastMailboxSyncStatus]
GO
ALTER TABLE [dbo].[MailboxFolderSyncState] ADD  CONSTRAINT [DF_MailboxFolderSyncState_IsEnabled]  DEFAULT ((1)) FOR [IsEnabled]
GO
ALTER TABLE [dbo].[MailboxFolderSyncState] ADD  CONSTRAINT [DF_MailboxFolderSyncState_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[MailboxFolderSyncState] ADD  CONSTRAINT [DF_MailboxFolderSyncState_CreatedUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedUtc]
GO
ALTER TABLE [dbo].[MailboxFolderSyncState] ADD  CONSTRAINT [DF_MailboxFolderSyncState_UpdatedUtc]  DEFAULT (sysutcdatetime()) FOR [UpdatedUtc]
GO
ALTER TABLE [dbo].[PublishedContact] ADD  CONSTRAINT [DF_PublishedContact_CompanyName]  DEFAULT ('') FOR [CompanyName]
GO
ALTER TABLE [dbo].[PublishedContact] ADD  CONSTRAINT [DF_PublishedContact_FilterSource]  DEFAULT ('Graph') FOR [FilterSource]
GO
ALTER TABLE [dbo].[PublishedContact] ADD  CONSTRAINT [DF_PublishedContact_IsEnabled]  DEFAULT ((1)) FOR [IsEnabled]
GO
ALTER TABLE [dbo].[PublishedContact] ADD  CONSTRAINT [DF_PublishedContact_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[PublishedContact] ADD  CONSTRAINT [DF_PublishedContact_IsManualOverride]  DEFAULT ((0)) FOR [IsManualOverride]
GO
ALTER TABLE [dbo].[PublishedContact] ADD  CONSTRAINT [DF_PublishedContact_CreatedUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedUtc]
GO
ALTER TABLE [dbo].[PublishedContact] ADD  CONSTRAINT [DF_PublishedContact_UpdatedUtc]  DEFAULT (sysutcdatetime()) FOR [UpdatedUtc]
GO
ALTER TABLE [dbo].[SyncExclusion] ADD  CONSTRAINT [DF_SyncExclusion_IsEnabled]  DEFAULT ((1)) FOR [IsEnabled]
GO
ALTER TABLE [dbo].[SyncExclusion] ADD  CONSTRAINT [DF_SyncExclusion_CreatedUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedUtc]
GO
ALTER TABLE [dbo].[SyncExclusion] ADD  CONSTRAINT [DF_SyncExclusion_UpdatedUtc]  DEFAULT (sysutcdatetime()) FOR [UpdatedUtc]
GO
ALTER TABLE [dbo].[SyncRun] ADD  CONSTRAINT [DF_SyncRun_StartedUtc]  DEFAULT (sysutcdatetime()) FOR [StartedUtc]
GO
ALTER TABLE [dbo].[SyncRun] ADD  CONSTRAINT [DF_SyncRun_IsDryRun]  DEFAULT ((0)) FOR [IsDryRun]
GO
ALTER TABLE [dbo].[SyncRun] ADD  CONSTRAINT [DF_SyncRun_TargetMailboxCount]  DEFAULT ((0)) FOR [TargetMailboxCount]
GO
ALTER TABLE [dbo].[SyncRun] ADD  CONSTRAINT [DF_SyncRun_PublishedContactCount]  DEFAULT ((0)) FOR [PublishedContactCount]
GO
ALTER TABLE [dbo].[SyncRun] ADD  CONSTRAINT [DF_SyncRun_CreatedCount]  DEFAULT ((0)) FOR [CreatedCount]
GO
ALTER TABLE [dbo].[SyncRun] ADD  CONSTRAINT [DF_SyncRun_UpdatedCount]  DEFAULT ((0)) FOR [UpdatedCount]
GO
ALTER TABLE [dbo].[SyncRun] ADD  CONSTRAINT [DF_SyncRun_DeletedCount]  DEFAULT ((0)) FOR [DeletedCount]
GO
ALTER TABLE [dbo].[SyncRun] ADD  CONSTRAINT [DF_SyncRun_SkippedCount]  DEFAULT ((0)) FOR [SkippedCount]
GO
ALTER TABLE [dbo].[SyncRun] ADD  CONSTRAINT [DF_SyncRun_ErrorCount]  DEFAULT ((0)) FOR [ErrorCount]
GO
ALTER TABLE [dbo].[SyncRun] ADD  CONSTRAINT [DF_SyncRun_CreatedUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedUtc]
GO
ALTER TABLE [dbo].[SyncRun] ADD  CONSTRAINT [DF_SyncRun_UpdatedUtc]  DEFAULT (sysutcdatetime()) FOR [UpdatedUtc]
GO
ALTER TABLE [dbo].[SyncRunItem] ADD  CONSTRAINT [DF_SyncRunItem_CreatedUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedUtc]
GO
ALTER TABLE [dbo].[SyncSettings] ADD  CONSTRAINT [DF_SyncSettings_ValueType]  DEFAULT ('String') FOR [ValueType]
GO
ALTER TABLE [dbo].[SyncSettings] ADD  CONSTRAINT [DF_SyncSettings_IsEnabled]  DEFAULT ((1)) FOR [IsEnabled]
GO
ALTER TABLE [dbo].[SyncSettings] ADD  CONSTRAINT [DF_SyncSettings_CreatedUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedUtc]
GO
ALTER TABLE [dbo].[SyncSettings] ADD  CONSTRAINT [DF_SyncSettings_UpdatedUtc]  DEFAULT (sysutcdatetime()) FOR [UpdatedUtc]
GO
ALTER TABLE [dbo].[TargetMailbox] ADD  CONSTRAINT [DF_TargetMailbox_FilterSource]  DEFAULT ('Sql') FOR [FilterSource]
GO
ALTER TABLE [dbo].[TargetMailbox] ADD  CONSTRAINT [DF_TargetMailbox_IsEnabled]  DEFAULT ((1)) FOR [IsEnabled]
GO
ALTER TABLE [dbo].[TargetMailbox] ADD  CONSTRAINT [DF_TargetMailbox_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[TargetMailbox] ADD  CONSTRAINT [DF_TargetMailbox_CreatedUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedUtc]
GO
ALTER TABLE [dbo].[TargetMailbox] ADD  CONSTRAINT [DF_TargetMailbox_UpdatedUtc]  DEFAULT (sysutcdatetime()) FOR [UpdatedUtc]
GO
ALTER TABLE [dbo].[ContactSyncState]  WITH CHECK ADD  CONSTRAINT [FK_ContactSyncState_TargetMailbox] FOREIGN KEY([TargetMailboxId])
REFERENCES [dbo].[TargetMailbox] ([TargetMailboxId])
GO
ALTER TABLE [dbo].[ContactSyncState] CHECK CONSTRAINT [FK_ContactSyncState_TargetMailbox]
GO
ALTER TABLE [dbo].[SyncRunItem]  WITH CHECK ADD  CONSTRAINT [FK_SyncRunItem_SyncRun] FOREIGN KEY([SyncRunId])
REFERENCES [dbo].[SyncRun] ([SyncRunId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[SyncRunItem] CHECK CONSTRAINT [FK_SyncRunItem_SyncRun]
GO
ALTER TABLE [dbo].[MailboxFolderSyncState]  WITH CHECK ADD  CONSTRAINT [CK_MailboxFolderSyncState_LastMailboxSyncStatus] CHECK  (([LastMailboxSyncStatus]='Failed' OR [LastMailboxSyncStatus]='CompletedWithErrors' OR [LastMailboxSyncStatus]='Success' OR [LastMailboxSyncStatus]='Running' OR [LastMailboxSyncStatus]='Unknown'))
GO
ALTER TABLE [dbo].[MailboxFolderSyncState] CHECK CONSTRAINT [CK_MailboxFolderSyncState_LastMailboxSyncStatus]
GO
ALTER TABLE [dbo].[MailboxFolderSyncState]  WITH CHECK ADD  CONSTRAINT [CK_MailboxFolderSyncState_LegacyFolderCleanupStatus] CHECK  (([LegacyFolderCleanupStatus] IS NULL OR ([LegacyFolderCleanupStatus]='Failed' OR [LegacyFolderCleanupStatus]='Deleted' OR [LegacyFolderCleanupStatus]='SkippedDryRun' OR [LegacyFolderCleanupStatus]='NotFound')))
GO
ALTER TABLE [dbo].[MailboxFolderSyncState] CHECK CONSTRAINT [CK_MailboxFolderSyncState_LegacyFolderCleanupStatus]
GO
ALTER TABLE [dbo].[SyncRun]  WITH CHECK ADD  CONSTRAINT [CK_SyncRun_Status] CHECK  (([Status]='Failed' OR [Status]='CompletedWithErrors' OR [Status]='Completed' OR [Status]='Running'))
GO
ALTER TABLE [dbo].[SyncRun] CHECK CONSTRAINT [CK_SyncRun_Status]
GO
ALTER TABLE [dbo].[SyncRunItem]  WITH CHECK ADD  CONSTRAINT [CK_SyncRunItem_Action] CHECK  (([Action]='Cleanup' OR [Action]='Error' OR [Action]='Skip' OR [Action]='Delete' OR [Action]='Update' OR [Action]='Create' OR [Action]='None'))
GO
ALTER TABLE [dbo].[SyncRunItem] CHECK CONSTRAINT [CK_SyncRunItem_Action]
GO
ALTER TABLE [dbo].[SyncRunItem]  WITH CHECK ADD  CONSTRAINT [CK_SyncRunItem_Result] CHECK  (([Result]='Skipped' OR [Result]='Failed' OR [Result]='Success' OR [Result]='Planned'))
GO
ALTER TABLE [dbo].[SyncRunItem] CHECK CONSTRAINT [CK_SyncRunItem_Result]
GO
/****** Object:  StoredProcedure [dbo].[usp_PurgeSyncRunHistory]    Script Date: 2026-05-28 12:42:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE   PROCEDURE [dbo].[usp_PurgeSyncRunHistory]
    @RetainDays int = 90
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.SyncRun
    WHERE StartedUtc < DATEADD(DAY, -@RetainDays, SYSUTCDATETIME())
      AND Status IN ('Completed', 'CompletedWithErrors', 'Failed');

    SELECT @@ROWCOUNT AS PurgedSyncRunCount;
END

GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Maps each source Entra user object ID to the contact item created in a target mailbox. Used to avoid duplicates, detect changes, and delete out-of-scope managed contacts.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'ContactSyncState'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional mapping metadata for documenting or extending source-to-contact field transformations without code changes for simple field enablement decisions.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'FieldMapping'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'One row per target mailbox tracking managed folder ID, mailbox-level sync status, and legacy folder cleanup result.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'MailboxFolderSyncState'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Cached source contacts selected from Microsoft Graph. SourceUserObjectId is the stable key used for mailbox contact matching.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PublishedContact'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Operational exclusion list. Supported types include SourceUserObjectId, UserPrincipalName, Mail, MailDomain, TargetMailboxUpn, and TargetMailboxMail.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SyncExclusion'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'One row per worker execution with summary counts and final status.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SyncRun'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Per-mailbox and per-contact action log for auditing, troubleshooting, and dry-run review.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SyncRunItem'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Application-level settings that can override appsettings.json for operational toggles such as DryRun and DeleteOutOfScopeContacts.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'SyncSettings'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Mailboxes that should receive the managed directory contacts. This table can be used alone or combined with an Entra security group.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'TargetMailbox'
GO




SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

MERGE dbo.SyncSettings AS target
USING
(
    VALUES
        (N'DryRun', N'true', N'Boolean', N'When true, mailbox contacts are not created, updated, or deleted. SyncRun and SyncRunItem records are still written.'),
        (N'DeleteOutOfScopeContacts', N'false', N'Boolean', N'When true, managed contacts for removed, disabled, excluded, or out-of-scope source users are deleted from the managed folder.'),
        (N'TargetAllUserMailboxes', N'false', N'Boolean', N'When true, all enabled Graph mail users are targeted except excluded mailboxes.'),
        (N'ManagedContactFolderName', N'ContactRelay', N'String', N'Dedicated contact folder name used in each target mailbox.'),
        (N'ManagedCategory', N'ContactRelay Managed', N'String', N'Outlook contact category used to identify contacts managed by this service.'),
        (N'DeleteLegacyFolderAfterSuccessfulSync', N'false', N'Boolean', N'When true, legacy managed folders are deleted only after a mailbox sync completes successfully.'),
        (N'LegacyContactFolderName', N'', N'String', N'Legacy contact folder name to delete after successful sync. Set this to the name of your old contact folder if you are migrating from another tool.'),
        (N'LegacyManagedFolderNames', N'', N'StringList', N'Comma-separated legacy contact folder names to delete after successful sync.')
) AS source (SettingKey, SettingValue, ValueType, Description)
ON target.SettingKey = source.SettingKey
WHEN MATCHED THEN
    UPDATE SET
        SettingValue = source.SettingValue,
        ValueType = source.ValueType,
        Description = source.Description,
        IsEnabled = 1,
        UpdatedUtc = SYSUTCDATETIME()
WHEN NOT MATCHED BY TARGET THEN
    INSERT (SettingKey, SettingValue, ValueType, Description, IsEnabled, CreatedUtc, UpdatedUtc)
    VALUES (source.SettingKey, source.SettingValue, source.ValueType, source.Description, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
GO

MERGE dbo.FieldMapping AS target
USING
(
    VALUES
        (N'givenName', N'Contact.GivenName', NULL),
        (N'surname', N'Contact.Surname', NULL),
        (N'jobTitle', N'Contact.JobTitle', NULL),
        (N'department', N'Contact.Department', NULL),
        (N'companyName', N'Contact.CompanyName', NULL),
        (N'mobilePhone', N'Contact.MobilePhone', N'PreferredPhone'),
        (N'businessPhones[0]', N'Contact.BusinessPhones[0]', NULL),
        (N'mail', N'Contact.EmailAddresses[0].Address', NULL)
) AS source (SourceField, TargetField, TransformName)
ON target.SourceField = source.SourceField AND target.TargetField = source.TargetField
WHEN MATCHED THEN
    UPDATE SET TransformName = source.TransformName, IsEnabled = 1, UpdatedUtc = SYSUTCDATETIME()
WHEN NOT MATCHED BY TARGET THEN
    INSERT (SourceField, TargetField, TransformName, IsEnabled, CreatedUtc, UpdatedUtc)
    VALUES (source.SourceField, source.TargetField, source.TransformName, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
GO

UPDATE dbo.FieldMapping
SET IsEnabled = 0,
    UpdatedUtc = SYSUTCDATETIME()
WHERE TargetField LIKE N'Contact.PersonalNotes%';
GO
