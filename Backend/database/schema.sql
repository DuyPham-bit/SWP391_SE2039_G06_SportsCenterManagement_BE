IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[centers] (
        [Id] bigint NOT NULL IDENTITY,
        [name] nvarchar(150) NOT NULL,
        [address] nvarchar(255) NOT NULL,
        [phone] nvarchar(20) NULL,
        [email] nvarchar(150) NULL,
        [description] nvarchar(500) NULL,
        [opening_time] time NULL,
        [closing_time] time NULL,
        [status] nvarchar(30) NOT NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_centers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[permissions] (
        [Id] bigint NOT NULL IDENTITY,
        [code] nvarchar(100) NOT NULL,
        [name] nvarchar(100) NOT NULL,
        [description] nvarchar(255) NULL,
        CONSTRAINT [PK_permissions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[roles] (
        [Id] bigint NOT NULL IDENTITY,
        [name] nvarchar(50) NOT NULL,
        [description] nvarchar(255) NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[sports] (
        [Id] bigint NOT NULL IDENTITY,
        [name] nvarchar(100) NOT NULL,
        [description] nvarchar(500) NULL,
        [status] nvarchar(30) NOT NULL,
        CONSTRAINT [PK_sports] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[membership_packages] (
        [Id] bigint NOT NULL IDENTITY,
        [center_id] bigint NOT NULL,
        [name] nvarchar(150) NOT NULL,
        [description] nvarchar(500) NULL,
        [duration_days] int NOT NULL,
        [price] decimal(12,2) NOT NULL,
        [max_classes] int NULL,
        [access_type] nvarchar(50) NULL,
        [status] nvarchar(30) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_membership_packages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_membership_packages_centers_center_id] FOREIGN KEY ([center_id]) REFERENCES [dbo].[centers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[rooms] (
        [Id] bigint NOT NULL IDENTITY,
        [center_id] bigint NOT NULL,
        [name] nvarchar(100) NOT NULL,
        [room_type] nvarchar(100) NULL,
        [capacity] int NOT NULL,
        [location_description] nvarchar(255) NULL,
        [status] nvarchar(30) NOT NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_rooms] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_rooms_centers_center_id] FOREIGN KEY ([center_id]) REFERENCES [dbo].[centers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[role_permissions] (
        [role_id] bigint NOT NULL,
        [permission_id] bigint NOT NULL,
        CONSTRAINT [PK_role_permissions] PRIMARY KEY ([role_id], [permission_id]),
        CONSTRAINT [FK_role_permissions_permissions_permission_id] FOREIGN KEY ([permission_id]) REFERENCES [dbo].[permissions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_role_permissions_roles_role_id] FOREIGN KEY ([role_id]) REFERENCES [dbo].[roles] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[users] (
        [Id] bigint NOT NULL IDENTITY,
        [username] nvarchar(100) NOT NULL,
        [email] nvarchar(150) NOT NULL,
        [password_hash] nvarchar(255) NOT NULL,
        [phone] nvarchar(20) NULL,
        [role_id] bigint NOT NULL,
        [status] nvarchar(30) NOT NULL,
        [last_login_at] datetime2 NULL,
        [failed_login_attempts] int NOT NULL,
        [locked_until] datetime2 NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_users_roles_role_id] FOREIGN KEY ([role_id]) REFERENCES [dbo].[roles] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[exercises] (
        [Id] bigint NOT NULL IDENTITY,
        [sport_id] bigint NULL,
        [name] nvarchar(150) NOT NULL,
        [description] nvarchar(1000) NULL,
        [muscle_group] nvarchar(100) NULL,
        [difficulty_level] nvarchar(50) NULL,
        [instructions] nvarchar(max) NULL,
        [video_url] nvarchar(500) NULL,
        [status] nvarchar(30) NOT NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_exercises] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_exercises_sports_sport_id] FOREIGN KEY ([sport_id]) REFERENCES [dbo].[sports] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[classes] (
        [Id] bigint NOT NULL IDENTITY,
        [center_id] bigint NOT NULL,
        [sport_id] bigint NOT NULL,
        [room_id] bigint NULL,
        [name] nvarchar(150) NOT NULL,
        [description] nvarchar(1000) NULL,
        [level] nvarchar(50) NULL,
        [capacity] int NOT NULL,
        [duration_minutes] int NOT NULL,
        [status] nvarchar(30) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_classes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_classes_centers_center_id] FOREIGN KEY ([center_id]) REFERENCES [dbo].[centers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_classes_rooms_room_id] FOREIGN KEY ([room_id]) REFERENCES [dbo].[rooms] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_classes_sports_sport_id] FOREIGN KEY ([sport_id]) REFERENCES [dbo].[sports] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[audit_logs] (
        [Id] bigint NOT NULL IDENTITY,
        [user_id] bigint NULL,
        [action] nvarchar(100) NOT NULL,
        [entity_type] nvarchar(100) NOT NULL,
        [entity_id] bigint NULL,
        [old_values] nvarchar(max) NULL,
        [new_values] nvarchar(max) NULL,
        [ip_address] nvarchar(45) NULL,
        [user_agent] nvarchar(500) NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_audit_logs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_audit_logs_users_user_id] FOREIGN KEY ([user_id]) REFERENCES [dbo].[users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[coach_profiles] (
        [Id] bigint NOT NULL IDENTITY,
        [user_id] bigint NOT NULL,
        [center_id] bigint NOT NULL,
        [coach_code] nvarchar(50) NOT NULL,
        [full_name] nvarchar(150) NOT NULL,
        [specialization] nvarchar(255) NULL,
        [certification] nvarchar(500) NULL,
        [experience_years] int NULL,
        [bio] nvarchar(max) NULL,
        [hire_date] date NULL,
        [status] nvarchar(30) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_coach_profiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_coach_profiles_centers_center_id] FOREIGN KEY ([center_id]) REFERENCES [dbo].[centers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_coach_profiles_users_user_id] FOREIGN KEY ([user_id]) REFERENCES [dbo].[users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[member_profiles] (
        [Id] bigint NOT NULL IDENTITY,
        [user_id] bigint NOT NULL,
        [member_code] nvarchar(50) NOT NULL,
        [full_name] nvarchar(150) NOT NULL,
        [date_of_birth] date NULL,
        [gender] nvarchar(20) NULL,
        [address] nvarchar(255) NULL,
        [emergency_contact_name] nvarchar(150) NULL,
        [emergency_contact_phone] nvarchar(20) NULL,
        [fitness_goal] nvarchar(500) NULL,
        [fitness_level] nvarchar(50) NULL,
        [medical_note] nvarchar(1000) NULL,
        [height_cm] decimal(5,2) NULL,
        [weight_kg] decimal(5,2) NULL,
        [booking_suspended_until] datetime2 NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_member_profiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_member_profiles_users_user_id] FOREIGN KEY ([user_id]) REFERENCES [dbo].[users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[notifications] (
        [Id] bigint NOT NULL IDENTITY,
        [sender_id] bigint NOT NULL,
        [title] nvarchar(200) NOT NULL,
        [message] nvarchar(max) NOT NULL,
        [notification_type] nvarchar(50) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [scheduled_at] datetime2 NULL,
        CONSTRAINT [PK_notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_notifications_users_sender_id] FOREIGN KEY ([sender_id]) REFERENCES [dbo].[users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[password_reset_tokens] (
        [Id] bigint NOT NULL IDENTITY,
        [user_id] bigint NOT NULL,
        [token] nvarchar(255) NOT NULL,
        [expires_at] datetime2 NOT NULL,
        [used_at] datetime2 NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_password_reset_tokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_password_reset_tokens_users_user_id] FOREIGN KEY ([user_id]) REFERENCES [dbo].[users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[staff_profiles] (
        [Id] bigint NOT NULL IDENTITY,
        [user_id] bigint NOT NULL,
        [center_id] bigint NOT NULL,
        [staff_code] nvarchar(50) NOT NULL,
        [full_name] nvarchar(150) NOT NULL,
        [position] nvarchar(100) NOT NULL,
        [hire_date] date NULL,
        [status] nvarchar(30) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_staff_profiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_staff_profiles_centers_center_id] FOREIGN KEY ([center_id]) REFERENCES [dbo].[centers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_staff_profiles_users_user_id] FOREIGN KEY ([user_id]) REFERENCES [dbo].[users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[system_settings] (
        [Id] bigint NOT NULL IDENTITY,
        [setting_key] nvarchar(100) NOT NULL,
        [setting_value] nvarchar(max) NULL,
        [description] nvarchar(500) NULL,
        [updated_by] bigint NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_system_settings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_system_settings_users_updated_by] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[class_schedules] (
        [Id] bigint NOT NULL IDENTITY,
        [class_id] bigint NOT NULL,
        [room_id] bigint NULL,
        [day_of_week] int NOT NULL,
        [start_time] time NOT NULL,
        [end_time] time NOT NULL,
        [start_date] date NULL,
        [end_date] date NULL,
        [status] nvarchar(30) NOT NULL,
        CONSTRAINT [PK_class_schedules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_class_schedules_classes_class_id] FOREIGN KEY ([class_id]) REFERENCES [dbo].[classes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_class_schedules_rooms_room_id] FOREIGN KEY ([room_id]) REFERENCES [dbo].[rooms] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[class_coaches] (
        [class_id] bigint NOT NULL,
        [coach_id] bigint NOT NULL,
        [assigned_date] date NULL,
        [is_primary] bit NOT NULL,
        [Id] bigint NOT NULL,
        CONSTRAINT [PK_class_coaches] PRIMARY KEY ([class_id], [coach_id]),
        CONSTRAINT [FK_class_coaches_classes_class_id] FOREIGN KEY ([class_id]) REFERENCES [dbo].[classes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_class_coaches_coach_profiles_coach_id] FOREIGN KEY ([coach_id]) REFERENCES [dbo].[coach_profiles] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[ai_conversations] (
        [Id] bigint NOT NULL IDENTITY,
        [user_id] bigint NOT NULL,
        [member_id] bigint NULL,
        [title] nvarchar(200) NOT NULL,
        [conversation_type] nvarchar(50) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NOT NULL,
        CONSTRAINT [PK_ai_conversations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ai_conversations_member_profiles_member_id] FOREIGN KEY ([member_id]) REFERENCES [dbo].[member_profiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ai_conversations_users_user_id] FOREIGN KEY ([user_id]) REFERENCES [dbo].[users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[assignments] (
        [Id] bigint NOT NULL IDENTITY,
        [coach_id] bigint NULL,
        [class_id] bigint NULL,
        [member_id] bigint NULL,
        [title] nvarchar(200) NOT NULL,
        [description] nvarchar(max) NULL,
        [due_date] datetime2 NULL,
        [created_at] datetime2 NOT NULL,
        [status] nvarchar(30) NOT NULL,
        CONSTRAINT [PK_assignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_assignments_classes_class_id] FOREIGN KEY ([class_id]) REFERENCES [dbo].[classes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_assignments_coach_profiles_coach_id] FOREIGN KEY ([coach_id]) REFERENCES [dbo].[coach_profiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_assignments_member_profiles_member_id] FOREIGN KEY ([member_id]) REFERENCES [dbo].[member_profiles] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[center_checkins] (
        [Id] bigint NOT NULL IDENTITY,
        [member_id] bigint NOT NULL,
        [center_id] bigint NOT NULL,
        [checked_in_by] bigint NULL,
        [check_in_time] datetime2 NOT NULL,
        [check_out_time] datetime2 NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_center_checkins] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_center_checkins_centers_center_id] FOREIGN KEY ([center_id]) REFERENCES [dbo].[centers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_center_checkins_member_profiles_member_id] FOREIGN KEY ([member_id]) REFERENCES [dbo].[member_profiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_center_checkins_users_checked_in_by] FOREIGN KEY ([checked_in_by]) REFERENCES [dbo].[users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[class_waitlist] (
        [Id] bigint NOT NULL IDENTITY,
        [class_id] bigint NOT NULL,
        [member_id] bigint NOT NULL,
        [joined_at] datetime2 NOT NULL,
        [status] nvarchar(30) NOT NULL,
        CONSTRAINT [PK_class_waitlist] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_class_waitlist_classes_class_id] FOREIGN KEY ([class_id]) REFERENCES [dbo].[classes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_class_waitlist_member_profiles_member_id] FOREIGN KEY ([member_id]) REFERENCES [dbo].[member_profiles] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[invoices] (
        [Id] bigint NOT NULL IDENTITY,
        [invoice_number] nvarchar(50) NOT NULL,
        [member_id] bigint NOT NULL,
        [center_id] bigint NOT NULL,
        [created_by] bigint NULL,
        [subtotal] decimal(12,2) NOT NULL,
        [discount] decimal(12,2) NOT NULL,
        [tax] decimal(12,2) NOT NULL,
        [total_amount] decimal(12,2) NOT NULL,
        [status] nvarchar(30) NOT NULL,
        [issued_at] datetime2 NOT NULL,
        [paid_at] datetime2 NULL,
        CONSTRAINT [PK_invoices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_invoices_centers_center_id] FOREIGN KEY ([center_id]) REFERENCES [dbo].[centers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_invoices_member_profiles_member_id] FOREIGN KEY ([member_id]) REFERENCES [dbo].[member_profiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_invoices_users_created_by] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[member_subscriptions] (
        [Id] bigint NOT NULL IDENTITY,
        [member_id] bigint NOT NULL,
        [package_id] bigint NOT NULL,
        [start_date] date NOT NULL,
        [end_date] date NOT NULL,
        [price] decimal(12,2) NOT NULL,
        [status] nvarchar(30) NOT NULL,
        [auto_renew] bit NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_member_subscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_member_subscriptions_member_profiles_member_id] FOREIGN KEY ([member_id]) REFERENCES [dbo].[member_profiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_member_subscriptions_membership_packages_package_id] FOREIGN KEY ([package_id]) REFERENCES [dbo].[membership_packages] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[support_requests] (
        [Id] bigint NOT NULL IDENTITY,
        [member_id] bigint NOT NULL,
        [handled_by] bigint NULL,
        [subject] nvarchar(200) NOT NULL,
        [description] nvarchar(max) NOT NULL,
        [priority] nvarchar(30) NOT NULL,
        [status] nvarchar(30) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [resolved_at] datetime2 NULL,
        CONSTRAINT [PK_support_requests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_support_requests_member_profiles_member_id] FOREIGN KEY ([member_id]) REFERENCES [dbo].[member_profiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_support_requests_users_handled_by] FOREIGN KEY ([handled_by]) REFERENCES [dbo].[users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[training_plans] (
        [Id] bigint NOT NULL IDENTITY,
        [member_id] bigint NOT NULL,
        [class_id] bigint NULL,
        [coach_id] bigint NOT NULL,
        [name] nvarchar(150) NOT NULL,
        [description] nvarchar(1000) NULL,
        [goal] nvarchar(500) NULL,
        [start_date] date NOT NULL,
        [end_date] date NULL,
        [plan_type] nvarchar(30) NOT NULL,
        [status] nvarchar(30) NOT NULL,
        [created_at] datetime2 NOT NULL,
        [updated_at] datetime2 NULL,
        CONSTRAINT [PK_training_plans] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_training_plans_classes_class_id] FOREIGN KEY ([class_id]) REFERENCES [dbo].[classes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_training_plans_coach_profiles_coach_id] FOREIGN KEY ([coach_id]) REFERENCES [dbo].[coach_profiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_training_plans_member_profiles_member_id] FOREIGN KEY ([member_id]) REFERENCES [dbo].[member_profiles] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[user_notifications] (
        [Id] bigint NOT NULL IDENTITY,
        [notification_id] bigint NOT NULL,
        [user_id] bigint NOT NULL,
        [is_read] bit NOT NULL,
        [read_at] datetime2 NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_user_notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_user_notifications_notifications_notification_id] FOREIGN KEY ([notification_id]) REFERENCES [dbo].[notifications] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_user_notifications_users_user_id] FOREIGN KEY ([user_id]) REFERENCES [dbo].[users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[class_sessions] (
        [Id] bigint NOT NULL IDENTITY,
        [class_id] bigint NOT NULL,
        [schedule_id] bigint NULL,
        [room_id] bigint NULL,
        [coach_id] bigint NULL,
        [session_date] date NOT NULL,
        [start_time] time NOT NULL,
        [end_time] time NOT NULL,
        [session_status] nvarchar(30) NOT NULL,
        [notes] nvarchar(1000) NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_class_sessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_class_sessions_class_schedules_schedule_id] FOREIGN KEY ([schedule_id]) REFERENCES [dbo].[class_schedules] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_class_sessions_classes_class_id] FOREIGN KEY ([class_id]) REFERENCES [dbo].[classes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_class_sessions_coach_profiles_coach_id] FOREIGN KEY ([coach_id]) REFERENCES [dbo].[coach_profiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_class_sessions_rooms_room_id] FOREIGN KEY ([room_id]) REFERENCES [dbo].[rooms] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[ai_messages] (
        [Id] bigint NOT NULL IDENTITY,
        [conversation_id] bigint NOT NULL,
        [sender_type] nvarchar(30) NOT NULL,
        [message] nvarchar(max) NOT NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_ai_messages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ai_messages_ai_conversations_conversation_id] FOREIGN KEY ([conversation_id]) REFERENCES [dbo].[ai_conversations] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[assignment_submissions] (
        [Id] bigint NOT NULL IDENTITY,
        [assignment_id] bigint NOT NULL,
        [member_id] bigint NOT NULL,
        [submitted_at] datetime2 NULL,
        [content] nvarchar(max) NULL,
        [score] decimal(5,2) NULL,
        [coach_feedback] nvarchar(1000) NULL,
        [status] nvarchar(30) NOT NULL,
        CONSTRAINT [PK_assignment_submissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_assignment_submissions_assignments_assignment_id] FOREIGN KEY ([assignment_id]) REFERENCES [dbo].[assignments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_assignment_submissions_member_profiles_member_id] FOREIGN KEY ([member_id]) REFERENCES [dbo].[member_profiles] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[payments] (
        [Id] bigint NOT NULL IDENTITY,
        [invoice_id] bigint NOT NULL,
        [member_id] bigint NOT NULL,
        [processed_by] bigint NULL,
        [payment_method] nvarchar(50) NOT NULL,
        [transaction_code] nvarchar(150) NULL,
        [amount] decimal(12,2) NOT NULL,
        [payment_status] nvarchar(30) NOT NULL,
        [paid_at] datetime2 NOT NULL,
        [note] nvarchar(500) NULL,
        [refund_approved_by] bigint NULL,
        CONSTRAINT [PK_payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_payments_invoices_invoice_id] FOREIGN KEY ([invoice_id]) REFERENCES [dbo].[invoices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_payments_member_profiles_member_id] FOREIGN KEY ([member_id]) REFERENCES [dbo].[member_profiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_payments_users_processed_by] FOREIGN KEY ([processed_by]) REFERENCES [dbo].[users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_payments_users_refund_approved_by] FOREIGN KEY ([refund_approved_by]) REFERENCES [dbo].[users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[class_enrollments] (
        [Id] bigint NOT NULL IDENTITY,
        [class_id] bigint NOT NULL,
        [member_id] bigint NOT NULL,
        [subscription_id] bigint NULL,
        [registered_by] bigint NULL,
        [registered_at] datetime2 NOT NULL,
        [cancelled_at] datetime2 NULL,
        [status] nvarchar(30) NOT NULL,
        [cancellation_reason] nvarchar(500) NULL,
        CONSTRAINT [PK_class_enrollments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_class_enrollments_classes_class_id] FOREIGN KEY ([class_id]) REFERENCES [dbo].[classes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_class_enrollments_member_profiles_member_id] FOREIGN KEY ([member_id]) REFERENCES [dbo].[member_profiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_class_enrollments_member_subscriptions_subscription_id] FOREIGN KEY ([subscription_id]) REFERENCES [dbo].[member_subscriptions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_class_enrollments_users_registered_by] FOREIGN KEY ([registered_by]) REFERENCES [dbo].[users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[support_request_messages] (
        [Id] bigint NOT NULL IDENTITY,
        [request_id] bigint NOT NULL,
        [sender_id] bigint NOT NULL,
        [message] nvarchar(max) NOT NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_support_request_messages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_support_request_messages_support_requests_request_id] FOREIGN KEY ([request_id]) REFERENCES [dbo].[support_requests] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_support_request_messages_users_sender_id] FOREIGN KEY ([sender_id]) REFERENCES [dbo].[users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[ai_exercise_recommendations] (
        [Id] bigint NOT NULL IDENTITY,
        [member_id] bigint NOT NULL,
        [conversation_id] bigint NULL,
        [exercise_id] bigint NULL,
        [training_plan_id] bigint NULL,
        [requested_by] bigint NULL,
        [recommendation_reason] nvarchar(max) NULL,
        [target_goal] nvarchar(500) NULL,
        [difficulty_level] nvarchar(50) NULL,
        [ai_model] nvarchar(100) NULL,
        [confidence_score] decimal(5,4) NULL,
        [accepted] bit NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_ai_exercise_recommendations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ai_exercise_recommendations_ai_conversations_conversation_id] FOREIGN KEY ([conversation_id]) REFERENCES [dbo].[ai_conversations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ai_exercise_recommendations_exercises_exercise_id] FOREIGN KEY ([exercise_id]) REFERENCES [dbo].[exercises] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ai_exercise_recommendations_member_profiles_member_id] FOREIGN KEY ([member_id]) REFERENCES [dbo].[member_profiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ai_exercise_recommendations_training_plans_training_plan_id] FOREIGN KEY ([training_plan_id]) REFERENCES [dbo].[training_plans] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ai_exercise_recommendations_users_requested_by] FOREIGN KEY ([requested_by]) REFERENCES [dbo].[users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[member_progress_reviews] (
        [Id] bigint NOT NULL IDENTITY,
        [member_id] bigint NOT NULL,
        [coach_id] bigint NOT NULL,
        [training_plan_id] bigint NULL,
        [review_date] date NOT NULL,
        [progress_score] decimal(5,2) NULL,
        [weight] decimal(8,2) NULL,
        [body_fat_percentage] decimal(5,2) NULL,
        [review_note] nvarchar(2000) NULL,
        [next_goal] nvarchar(1000) NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_member_progress_reviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_member_progress_reviews_coach_profiles_coach_id] FOREIGN KEY ([coach_id]) REFERENCES [dbo].[coach_profiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_member_progress_reviews_member_profiles_member_id] FOREIGN KEY ([member_id]) REFERENCES [dbo].[member_profiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_member_progress_reviews_training_plans_training_plan_id] FOREIGN KEY ([training_plan_id]) REFERENCES [dbo].[training_plans] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[training_plan_exercises] (
        [Id] bigint NOT NULL IDENTITY,
        [training_plan_id] bigint NOT NULL,
        [exercise_id] bigint NOT NULL,
        [day_number] int NULL,
        [sets] int NULL,
        [repetitions] int NULL,
        [duration_seconds] int NULL,
        [rest_seconds] int NULL,
        [target_weight] decimal(8,2) NULL,
        [notes] nvarchar(1000) NULL,
        [sort_order] int NULL,
        CONSTRAINT [PK_training_plan_exercises] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_training_plan_exercises_exercises_exercise_id] FOREIGN KEY ([exercise_id]) REFERENCES [dbo].[exercises] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_training_plan_exercises_training_plans_training_plan_id] FOREIGN KEY ([training_plan_id]) REFERENCES [dbo].[training_plans] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[attendance] (
        [Id] bigint NOT NULL IDENTITY,
        [session_id] bigint NOT NULL,
        [member_id] bigint NOT NULL,
        [checked_in_by] bigint NULL,
        [attendance_status] nvarchar(30) NOT NULL,
        [check_in_time] datetime2 NULL,
        [check_out_time] datetime2 NULL,
        [note] nvarchar(500) NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_attendance] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_attendance_class_sessions_session_id] FOREIGN KEY ([session_id]) REFERENCES [dbo].[class_sessions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_attendance_member_profiles_member_id] FOREIGN KEY ([member_id]) REFERENCES [dbo].[member_profiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_attendance_users_checked_in_by] FOREIGN KEY ([checked_in_by]) REFERENCES [dbo].[users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[invoice_items] (
        [Id] bigint NOT NULL IDENTITY,
        [invoice_id] bigint NOT NULL,
        [package_id] bigint NULL,
        [subscription_id] bigint NULL,
        [enrollment_id] bigint NULL,
        [description] nvarchar(500) NOT NULL,
        [quantity] int NOT NULL,
        [unit_price] decimal(12,2) NOT NULL,
        [amount] decimal(12,2) NOT NULL,
        CONSTRAINT [PK_invoice_items] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_invoice_items_class_enrollments_enrollment_id] FOREIGN KEY ([enrollment_id]) REFERENCES [dbo].[class_enrollments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_invoice_items_invoices_invoice_id] FOREIGN KEY ([invoice_id]) REFERENCES [dbo].[invoices] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_invoice_items_member_subscriptions_subscription_id] FOREIGN KEY ([subscription_id]) REFERENCES [dbo].[member_subscriptions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_invoice_items_membership_packages_package_id] FOREIGN KEY ([package_id]) REFERENCES [dbo].[membership_packages] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[session_bookings] (
        [Id] bigint NOT NULL IDENTITY,
        [session_id] bigint NOT NULL,
        [member_id] bigint NOT NULL,
        [enrollment_id] bigint NULL,
        [booked_at] datetime2 NOT NULL,
        [status] nvarchar(30) NOT NULL,
        CONSTRAINT [PK_session_bookings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_session_bookings_class_enrollments_enrollment_id] FOREIGN KEY ([enrollment_id]) REFERENCES [dbo].[class_enrollments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_session_bookings_class_sessions_session_id] FOREIGN KEY ([session_id]) REFERENCES [dbo].[class_sessions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_session_bookings_member_profiles_member_id] FOREIGN KEY ([member_id]) REFERENCES [dbo].[member_profiles] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[training_results] (
        [Id] bigint NOT NULL IDENTITY,
        [member_id] bigint NOT NULL,
        [coach_id] bigint NOT NULL,
        [session_id] bigint NULL,
        [training_plan_exercise_id] bigint NULL,
        [exercise_id] bigint NULL,
        [training_plan_id] bigint NULL,
        [result_date] date NOT NULL,
        [sets_completed] int NULL,
        [repetitions_completed] int NULL,
        [weight] decimal(8,2) NULL,
        [duration_seconds] int NULL,
        [calories_burned] decimal(8,2) NULL,
        [performance_score] decimal(5,2) NULL,
        [notes] nvarchar(1000) NULL,
        [created_at] datetime2 NOT NULL,
        CONSTRAINT [PK_training_results] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_training_results_class_sessions_session_id] FOREIGN KEY ([session_id]) REFERENCES [dbo].[class_sessions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_training_results_coach_profiles_coach_id] FOREIGN KEY ([coach_id]) REFERENCES [dbo].[coach_profiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_training_results_exercises_exercise_id] FOREIGN KEY ([exercise_id]) REFERENCES [dbo].[exercises] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_training_results_member_profiles_member_id] FOREIGN KEY ([member_id]) REFERENCES [dbo].[member_profiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_training_results_training_plan_exercises_training_plan_exercise_id] FOREIGN KEY ([training_plan_exercise_id]) REFERENCES [dbo].[training_plan_exercises] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_training_results_training_plans_training_plan_id] FOREIGN KEY ([training_plan_id]) REFERENCES [dbo].[training_plans] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ai_conversations_member_id] ON [dbo].[ai_conversations] ([member_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ai_conversations_user_id] ON [dbo].[ai_conversations] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ai_exercise_recommendations_conversation_id] ON [dbo].[ai_exercise_recommendations] ([conversation_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ai_exercise_recommendations_exercise_id] ON [dbo].[ai_exercise_recommendations] ([exercise_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ai_exercise_recommendations_member_id] ON [dbo].[ai_exercise_recommendations] ([member_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ai_exercise_recommendations_requested_by] ON [dbo].[ai_exercise_recommendations] ([requested_by]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ai_exercise_recommendations_training_plan_id] ON [dbo].[ai_exercise_recommendations] ([training_plan_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ai_messages_conversation_id] ON [dbo].[ai_messages] ([conversation_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_assignment_submissions_assignment_id] ON [dbo].[assignment_submissions] ([assignment_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_assignment_submissions_member_id] ON [dbo].[assignment_submissions] ([member_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_assignments_class_id] ON [dbo].[assignments] ([class_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_assignments_coach_id] ON [dbo].[assignments] ([coach_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_assignments_member_id] ON [dbo].[assignments] ([member_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_attendance_checked_in_by] ON [dbo].[attendance] ([checked_in_by]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_attendance_member_id] ON [dbo].[attendance] ([member_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_attendance_session_id] ON [dbo].[attendance] ([session_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_audit_logs_user_id] ON [dbo].[audit_logs] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_center_checkins_center_id] ON [dbo].[center_checkins] ([center_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_center_checkins_checked_in_by] ON [dbo].[center_checkins] ([checked_in_by]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_center_checkins_member_id] ON [dbo].[center_checkins] ([member_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_class_coaches_coach_id] ON [dbo].[class_coaches] ([coach_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_class_enrollments_class_id] ON [dbo].[class_enrollments] ([class_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_class_enrollments_member_id] ON [dbo].[class_enrollments] ([member_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_class_enrollments_registered_by] ON [dbo].[class_enrollments] ([registered_by]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_class_enrollments_subscription_id] ON [dbo].[class_enrollments] ([subscription_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_class_schedules_class_id] ON [dbo].[class_schedules] ([class_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_class_schedules_room_id] ON [dbo].[class_schedules] ([room_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_class_sessions_class_id] ON [dbo].[class_sessions] ([class_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_class_sessions_coach_id] ON [dbo].[class_sessions] ([coach_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_class_sessions_room_id] ON [dbo].[class_sessions] ([room_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_class_sessions_schedule_id] ON [dbo].[class_sessions] ([schedule_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_class_waitlist_class_id] ON [dbo].[class_waitlist] ([class_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_class_waitlist_member_id] ON [dbo].[class_waitlist] ([member_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_classes_center_id] ON [dbo].[classes] ([center_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_classes_room_id] ON [dbo].[classes] ([room_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_classes_sport_id] ON [dbo].[classes] ([sport_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_coach_profiles_center_id] ON [dbo].[coach_profiles] ([center_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_coach_profiles_coach_code] ON [dbo].[coach_profiles] ([coach_code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_coach_profiles_user_id] ON [dbo].[coach_profiles] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_exercises_sport_id] ON [dbo].[exercises] ([sport_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_invoice_items_enrollment_id] ON [dbo].[invoice_items] ([enrollment_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_invoice_items_invoice_id] ON [dbo].[invoice_items] ([invoice_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_invoice_items_package_id] ON [dbo].[invoice_items] ([package_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_invoice_items_subscription_id] ON [dbo].[invoice_items] ([subscription_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_invoices_center_id] ON [dbo].[invoices] ([center_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_invoices_created_by] ON [dbo].[invoices] ([created_by]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_invoices_invoice_number] ON [dbo].[invoices] ([invoice_number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_invoices_member_id] ON [dbo].[invoices] ([member_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_member_profiles_member_code] ON [dbo].[member_profiles] ([member_code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_member_profiles_user_id] ON [dbo].[member_profiles] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_member_progress_reviews_coach_id] ON [dbo].[member_progress_reviews] ([coach_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_member_progress_reviews_member_id] ON [dbo].[member_progress_reviews] ([member_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_member_progress_reviews_training_plan_id] ON [dbo].[member_progress_reviews] ([training_plan_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_member_subscriptions_member_id] ON [dbo].[member_subscriptions] ([member_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_member_subscriptions_package_id] ON [dbo].[member_subscriptions] ([package_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_membership_packages_center_id] ON [dbo].[membership_packages] ([center_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_notifications_sender_id] ON [dbo].[notifications] ([sender_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_password_reset_tokens_token] ON [dbo].[password_reset_tokens] ([token]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_password_reset_tokens_user_id] ON [dbo].[password_reset_tokens] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_payments_invoice_id] ON [dbo].[payments] ([invoice_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_payments_member_id] ON [dbo].[payments] ([member_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_payments_processed_by] ON [dbo].[payments] ([processed_by]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_payments_refund_approved_by] ON [dbo].[payments] ([refund_approved_by]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_permissions_code] ON [dbo].[permissions] ([code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_role_permissions_permission_id] ON [dbo].[role_permissions] ([permission_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_roles_name] ON [dbo].[roles] ([name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_rooms_center_id] ON [dbo].[rooms] ([center_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_session_bookings_enrollment_id] ON [dbo].[session_bookings] ([enrollment_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_session_bookings_member_id] ON [dbo].[session_bookings] ([member_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_session_bookings_session_id] ON [dbo].[session_bookings] ([session_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_staff_profiles_center_id] ON [dbo].[staff_profiles] ([center_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_staff_profiles_staff_code] ON [dbo].[staff_profiles] ([staff_code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_staff_profiles_user_id] ON [dbo].[staff_profiles] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_support_request_messages_request_id] ON [dbo].[support_request_messages] ([request_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_support_request_messages_sender_id] ON [dbo].[support_request_messages] ([sender_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_support_requests_handled_by] ON [dbo].[support_requests] ([handled_by]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_support_requests_member_id] ON [dbo].[support_requests] ([member_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_system_settings_setting_key] ON [dbo].[system_settings] ([setting_key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_system_settings_updated_by] ON [dbo].[system_settings] ([updated_by]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_training_plan_exercises_exercise_id] ON [dbo].[training_plan_exercises] ([exercise_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_training_plan_exercises_training_plan_id] ON [dbo].[training_plan_exercises] ([training_plan_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_training_plans_class_id] ON [dbo].[training_plans] ([class_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_training_plans_coach_id] ON [dbo].[training_plans] ([coach_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_training_plans_member_id] ON [dbo].[training_plans] ([member_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_training_results_coach_id] ON [dbo].[training_results] ([coach_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_training_results_exercise_id] ON [dbo].[training_results] ([exercise_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_training_results_member_id] ON [dbo].[training_results] ([member_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_training_results_session_id] ON [dbo].[training_results] ([session_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_training_results_training_plan_exercise_id] ON [dbo].[training_results] ([training_plan_exercise_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_training_results_training_plan_id] ON [dbo].[training_results] ([training_plan_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_user_notifications_notification_id] ON [dbo].[user_notifications] ([notification_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_user_notifications_user_id] ON [dbo].[user_notifications] ([user_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_users_email] ON [dbo].[users] ([email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_users_role_id] ON [dbo].[users] ([role_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_users_username] ON [dbo].[users] ([username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929152157_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929152157_InitialCreate', N'10.0.12');
END;

COMMIT;
GO


BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929165224_CoreFlowUniqueness'
)
BEGIN
    DROP INDEX [IX_session_bookings_session_id] ON [dbo].[session_bookings];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929165224_CoreFlowUniqueness'
)
BEGIN
    DROP INDEX [IX_class_enrollments_class_id] ON [dbo].[class_enrollments];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929165224_CoreFlowUniqueness'
)
BEGIN
    CREATE UNIQUE INDEX [IX_session_bookings_session_id_member_id] ON [dbo].[session_bookings] ([session_id], [member_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929165224_CoreFlowUniqueness'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_payments_transaction_code] ON [dbo].[payments] ([transaction_code]) WHERE [transaction_code] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929165224_CoreFlowUniqueness'
)
BEGIN
    CREATE UNIQUE INDEX [IX_class_enrollments_class_id_member_id] ON [dbo].[class_enrollments] ([class_id], [member_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929165224_CoreFlowUniqueness'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929165224_CoreFlowUniqueness', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006090000_Flow3FinancialHardening'
)
BEGIN
    ALTER TABLE [dbo].[invoices] ADD [idempotency_key] nvarchar(100) NULL;
    ALTER TABLE [dbo].[payments] ADD [created_at] datetime2 NULL;
    ALTER TABLE [dbo].[payments] ADD [gateway_reference] nvarchar(100) NULL;
    ALTER TABLE [dbo].[payments] ADD [provider_transaction_id] nvarchar(150) NULL;
    ALTER TABLE [dbo].[payments] ADD [idempotency_key] nvarchar(100) NULL;
    ALTER TABLE [dbo].[payments] ADD [gateway_payment_url] nvarchar(2048) NULL;
    ALTER TABLE [dbo].[payments] ADD [amount_received] decimal(12,2) NULL;

    UPDATE [dbo].[payments]
    SET [created_at] = COALESCE([paid_at], SYSUTCDATETIME())
    WHERE [created_at] IS NULL;
    UPDATE [dbo].[payments]
    SET [payment_status] = N'Succeeded'
    WHERE [payment_status] = N'Completed';
    UPDATE [dbo].[invoices]
    SET [status] = N'Voided'
    WHERE [status] = N'Cancelled';
    ALTER TABLE [dbo].[payments] ALTER COLUMN [created_at] datetime2 NOT NULL;
    ALTER TABLE [dbo].[payments] ALTER COLUMN [paid_at] datetime2 NULL;

    CREATE TABLE [dbo].[payment_refunds] (
        [Id] bigint NOT NULL IDENTITY,
        [payment_id] bigint NOT NULL,
        [amount] decimal(12,2) NOT NULL,
        [status] nvarchar(30) NOT NULL,
        [idempotency_key] nvarchar(100) NOT NULL,
        [provider_refund_id] nvarchar(150) NULL,
        [external_reference] nvarchar(150) NULL,
        [reason] nvarchar(500) NOT NULL,
        [requested_by] bigint NOT NULL,
        [created_at] datetime2 NOT NULL,
        [processed_at] datetime2 NULL,
        CONSTRAINT [PK_payment_refunds] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_payment_refunds_payments_payment_id] FOREIGN KEY ([payment_id]) REFERENCES [dbo].[payments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_payment_refunds_users_requested_by] FOREIGN KEY ([requested_by]) REFERENCES [dbo].[users] ([Id]) ON DELETE NO ACTION
    );

    CREATE UNIQUE INDEX [IX_invoices_idempotency_key] ON [dbo].[invoices] ([idempotency_key]) WHERE [idempotency_key] IS NOT NULL;
    CREATE UNIQUE INDEX [IX_payments_gateway_reference] ON [dbo].[payments] ([gateway_reference]) WHERE [gateway_reference] IS NOT NULL;
    CREATE UNIQUE INDEX [IX_payments_provider_transaction_id] ON [dbo].[payments] ([provider_transaction_id]) WHERE [provider_transaction_id] IS NOT NULL;
    CREATE UNIQUE INDEX [IX_payments_idempotency_key] ON [dbo].[payments] ([idempotency_key]) WHERE [idempotency_key] IS NOT NULL;
    CREATE INDEX [IX_payment_refunds_payment_id] ON [dbo].[payment_refunds] ([payment_id]);
    CREATE INDEX [IX_payment_refunds_requested_by] ON [dbo].[payment_refunds] ([requested_by]);
    CREATE UNIQUE INDEX [IX_payment_refunds_idempotency_key] ON [dbo].[payment_refunds] ([idempotency_key]);
    CREATE UNIQUE INDEX [IX_payment_refunds_provider_refund_id] ON [dbo].[payment_refunds] ([provider_refund_id]) WHERE [provider_refund_id] IS NOT NULL;

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006090000_Flow3FinancialHardening', N'10.0.12');
END;
COMMIT;
GO
