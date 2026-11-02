-- =========================================================================================
-- HỆ THỐNG QUẢN LÝ TRUNG TÂM THỂ THAO SCMS (SPORTS CENTER MANAGEMENT SYSTEM)
-- FILE TỔNG HỢP DUY NHẤT: KHỞI TẠO CƠ SỞ DỮ LIỆU & DỮ LIỆU MẪU ĐẦY ĐỦ (FULL DATABASE & SEED)
-- =========================================================================================
-- Hướng dẫn:
-- 1. Mở file này trong SQL Server Management Studio (SSMS) hoặc Azure Data Studio.
-- 2. Đảm bảo kết nối tới SQL Server của bạn.
-- 3. Bấm "Execute" (hoặc nhấn phím F5) để chạy toàn bộ từ tạo Database, tạo Bảng tới nạp Data.
-- =========================================================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'SportsCenterManagement')
BEGIN
    CREATE DATABASE [SportsCenterManagement];
END;
GO

USE [SportsCenterManagement];
GO

-- =========================================================================================
-- PHẦN 1: CẤU TRÚC BẢNG, KHÓA NGOẠI, CHỈ MỤC & RÀNG BUỘC (SCHEMA DDL)
-- =========================================================================================

﻿IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
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
    WHERE [MigrationId] = N'20261001022613_PendingMembershipEffectiveDates'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[member_subscriptions]') AND [c].[name] = N'start_date');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [dbo].[member_subscriptions] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [dbo].[member_subscriptions] ALTER COLUMN [start_date] date NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001022613_PendingMembershipEffectiveDates'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[member_subscriptions]') AND [c].[name] = N'end_date');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[member_subscriptions] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [dbo].[member_subscriptions] ALTER COLUMN [end_date] date NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001022613_PendingMembershipEffectiveDates'
)
BEGIN
    ALTER TABLE [dbo].[member_subscriptions] ADD [duration_days] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001022613_PendingMembershipEffectiveDates'
)
BEGIN
    UPDATE subscription
    SET duration_days = package.duration_days
    FROM dbo.member_subscriptions AS subscription
    INNER JOIN dbo.membership_packages AS package
        ON package.id = subscription.package_id;

    UPDATE dbo.member_subscriptions
    SET start_date = NULL, end_date = NULL
    WHERE status = 'PendingPayment';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001022613_PendingMembershipEffectiveDates'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[dbo].[member_subscriptions]') AND [c].[name] = N'duration_days');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [dbo].[member_subscriptions] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [dbo].[member_subscriptions] ALTER COLUMN [duration_days] int NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001022613_PendingMembershipEffectiveDates'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001022613_PendingMembershipEffectiveDates', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001024850_Flow1MembershipScope'
)
BEGIN
    DROP INDEX [IX_membership_packages_center_id] ON [dbo].[membership_packages];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001024850_Flow1MembershipScope'
)
BEGIN
    ALTER TABLE [dbo].[member_profiles] ADD [center_id] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001024850_Flow1MembershipScope'
)
BEGIN
    WITH member_centers AS
    (
        SELECT member_id, MIN(center_id) AS center_id
        FROM dbo.invoices
        GROUP BY member_id
        HAVING COUNT(DISTINCT center_id) = 1
    )
    UPDATE profile
    SET center_id = member_centers.center_id
    FROM dbo.member_profiles AS profile
    INNER JOIN member_centers ON member_centers.member_id = profile.id;

    WITH subscription_centers AS
    (
        SELECT subscription.member_id, MIN(package.center_id) AS center_id
        FROM dbo.member_subscriptions AS subscription
        INNER JOIN dbo.membership_packages AS package ON package.id = subscription.package_id
        GROUP BY subscription.member_id
        HAVING COUNT(DISTINCT package.center_id) = 1
    )
    UPDATE profile
    SET center_id = subscription_centers.center_id
    FROM dbo.member_profiles AS profile
    INNER JOIN subscription_centers ON subscription_centers.member_id = profile.id
    WHERE profile.center_id IS NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.invoices AS invoice WHERE invoice.member_id = profile.id);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001024850_Flow1MembershipScope'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_users_phone] ON [dbo].[users] ([phone]) WHERE [phone] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001024850_Flow1MembershipScope'
)
BEGIN
    CREATE UNIQUE INDEX [IX_membership_packages_center_id_name] ON [dbo].[membership_packages] ([center_id], [name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001024850_Flow1MembershipScope'
)
BEGIN
    CREATE INDEX [IX_member_profiles_center_id] ON [dbo].[member_profiles] ([center_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001024850_Flow1MembershipScope'
)
BEGIN
    ALTER TABLE [dbo].[member_profiles] ADD CONSTRAINT [FK_member_profiles_centers_center_id] FOREIGN KEY ([center_id]) REFERENCES [dbo].[centers] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261001024850_Flow1MembershipScope'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261001024850_Flow1MembershipScope', N'10.0.12');
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


GO

-- =========================================================================================
-- PHẦN 2: DỮ LIỆU MẪU NỀN TẢNG (FLOW 2 DEMO DATA: LỚP HỌC, ĐẶT LỊCH, TÀI KHOẢN MẪU)
-- =========================================================================================

-- Local demo data for Flow 2: Class booking and schedule management.
-- Safe to re-run: records are owned by the FLOW2 identifiers below and are upserted.
-- Demo accounts intentionally share the disposable password Member@123456.
-- Never run this script against a production database.

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

BEGIN TRY
    DECLARE @UtcNow DATETIME2(0) = SYSUTCDATETIME();
    DECLARE @LocalNow DATETIME2(0) = DATEADD(HOUR, 7, @UtcNow);
    DECLARE @Today DATE = CAST(@LocalNow AS DATE);
    DECLARE @Tomorrow DATE = DATEADD(DAY, 1, @Today);
    DECLARE @TomorrowDayOfWeek INT = ((DATEDIFF(DAY, '19000107', @Tomorrow) % 7) + 7) % 7;
    DECLARE @PasswordHash NVARCHAR(255) = N'$2a$11$1k4jiQRrxN6VjiS3pvnSXuS4hCQMsackbN6sTYXVo46936W7DyZqe';

    DECLARE @CenterId BIGINT = (
        SELECT TOP (1) staff.center_id
        FROM dbo.staff_profiles AS staff
        INNER JOIN dbo.users AS account ON account.id = staff.user_id
        INNER JOIN dbo.centers AS center ON center.id = staff.center_id
        WHERE account.username = N'manager01' AND center.status = N'Active'
        ORDER BY staff.id
    );
    IF @CenterId IS NULL
        SELECT TOP (1) @CenterId = id FROM dbo.centers WHERE status = N'Active' ORDER BY id;
    IF @CenterId IS NULL
        THROW 51000, N'Không tìm thấy trung tâm Active để tạo dữ liệu demo Flow 2.', 1;

    DECLARE @ManagerRoleId BIGINT = (SELECT TOP (1) id FROM dbo.roles WHERE UPPER(name) = N'MANAGER');
    DECLARE @CoachRoleId BIGINT = (SELECT TOP (1) id FROM dbo.roles WHERE UPPER(name) = N'COACH');
    DECLARE @ReceptionRoleId BIGINT = (SELECT TOP (1) id FROM dbo.roles WHERE UPPER(name) = N'RECEPTIONIST');
    DECLARE @MemberRoleId BIGINT = (SELECT TOP (1) id FROM dbo.roles WHERE UPPER(name) = N'MEMBER');
    IF @ManagerRoleId IS NULL OR @CoachRoleId IS NULL OR @ReceptionRoleId IS NULL OR @MemberRoleId IS NULL
        THROW 51001, N'Thiếu một trong các role Manager, Coach, Receptionist hoặc Member.', 1;

    -- Older databases may already contain some role grants, causing the legacy initializer
    -- to skip later default grants. Ensure Manager can open the center audit screen used by Flow 2.
    DECLARE @AuditCenterReadPermissionId BIGINT = (
        SELECT TOP (1) id FROM dbo.permissions WHERE code = N'audit.center.read'
    );
    IF @AuditCenterReadPermissionId IS NULL
        THROW 51002, N'Thiếu permission audit.center.read trong database.', 1;
    IF NOT EXISTS (
        SELECT 1 FROM dbo.role_permissions
        WHERE role_id = @ManagerRoleId AND permission_id = @AuditCenterReadPermissionId
    )
        INSERT INTO dbo.role_permissions (role_id, permission_id)
        VALUES (@ManagerRoleId, @AuditCenterReadPermissionId);

    DECLARE @Accounts TABLE
    (
        username NVARCHAR(100) NOT NULL,
        email NVARCHAR(150) NOT NULL,
        role_id BIGINT NOT NULL
    );

    INSERT INTO @Accounts (username, email, role_id)
    VALUES
        (N'flow2.manager', N'flow2.manager@sportscenter.vn', @ManagerRoleId),
        (N'flow2.coach', N'flow2.coach@sportscenter.vn', @CoachRoleId),
        (N'flow2.reception', N'flow2.reception@sportscenter.vn', @ReceptionRoleId),
        (N'flow2.member.a', N'flow2.member.a@sportscenter.vn', @MemberRoleId),
        (N'flow2.member.b', N'flow2.member.b@sportscenter.vn', @MemberRoleId),
        (N'flow2.member.c', N'flow2.member.c@sportscenter.vn', @MemberRoleId),
        (N'flow2.member.expired', N'flow2.member.expired@sportscenter.vn', @MemberRoleId);

    MERGE dbo.users WITH (HOLDLOCK) AS target
    USING @Accounts AS source
       ON target.username = source.username
    WHEN MATCHED THEN
        UPDATE SET
            target.email = source.email,
            target.password_hash = @PasswordHash,
            target.role_id = source.role_id,
            target.status = N'Active',
            target.failed_login_attempts = 0,
            target.locked_until = NULL,
            target.updated_at = @UtcNow
    WHEN NOT MATCHED THEN
        INSERT (username, email, password_hash, role_id, status, auth_version, failed_login_attempts, created_at, updated_at)
        VALUES (source.username, source.email, @PasswordHash, source.role_id, N'Active', 0, 0, @UtcNow, @UtcNow);

    DECLARE @ManagerUserId BIGINT = (SELECT id FROM dbo.users WHERE username = N'flow2.manager');
    DECLARE @CoachUserId BIGINT = (SELECT id FROM dbo.users WHERE username = N'flow2.coach');
    DECLARE @ReceptionUserId BIGINT = (SELECT id FROM dbo.users WHERE username = N'flow2.reception');
    DECLARE @MemberAUserId BIGINT = (SELECT id FROM dbo.users WHERE username = N'flow2.member.a');
    DECLARE @MemberBUserId BIGINT = (SELECT id FROM dbo.users WHERE username = N'flow2.member.b');
    DECLARE @MemberCUserId BIGINT = (SELECT id FROM dbo.users WHERE username = N'flow2.member.c');
    DECLARE @ExpiredUserId BIGINT = (SELECT id FROM dbo.users WHERE username = N'flow2.member.expired');

    UPDATE dbo.staff_profiles
    SET center_id = @CenterId, staff_code = N'FLOW2-MANAGER', full_name = N'Nguyễn Minh Quản',
        position = N'Center Manager - Flow 2 Demo', hire_date = DATEADD(YEAR, -2, @Today),
        status = N'Active', updated_at = @UtcNow
    WHERE user_id = @ManagerUserId;
    IF @@ROWCOUNT = 0
        INSERT INTO dbo.staff_profiles
            (user_id, center_id, staff_code, full_name, position, hire_date, status, created_at, updated_at)
        VALUES
            (@ManagerUserId, @CenterId, N'FLOW2-MANAGER', N'Nguyễn Minh Quản', N'Center Manager - Flow 2 Demo',
             DATEADD(YEAR, -2, @Today), N'Active', @UtcNow, @UtcNow);

    UPDATE dbo.staff_profiles
    SET center_id = @CenterId, staff_code = N'FLOW2-RECEPTION', full_name = N'Trần Thu Lễ',
        position = N'Receptionist - Flow 2 Demo', hire_date = DATEADD(YEAR, -1, @Today),
        status = N'Active', updated_at = @UtcNow
    WHERE user_id = @ReceptionUserId;
    IF @@ROWCOUNT = 0
        INSERT INTO dbo.staff_profiles
            (user_id, center_id, staff_code, full_name, position, hire_date, status, created_at, updated_at)
        VALUES
            (@ReceptionUserId, @CenterId, N'FLOW2-RECEPTION', N'Trần Thu Lễ', N'Receptionist - Flow 2 Demo',
             DATEADD(YEAR, -1, @Today), N'Active', @UtcNow, @UtcNow);

    UPDATE dbo.coach_profiles
    SET center_id = @CenterId, coach_code = N'FLOW2-COACH', full_name = N'Lê Hoàng Yoga',
        specialization = N'Yoga, Pilates và phục hồi vận động', certification = N'RYT-500, CPT',
        experience_years = 6, bio = N'HLV demo phụ trách toàn bộ các lớp của Flow 2.',
        hire_date = DATEADD(YEAR, -3, @Today), status = N'Active', updated_at = @UtcNow
    WHERE user_id = @CoachUserId;
    IF @@ROWCOUNT = 0
        INSERT INTO dbo.coach_profiles
            (user_id, center_id, coach_code, full_name, specialization, certification, experience_years,
             bio, hire_date, status, created_at, updated_at)
        VALUES
            (@CoachUserId, @CenterId, N'FLOW2-COACH', N'Lê Hoàng Yoga', N'Yoga, Pilates và phục hồi vận động',
             N'RYT-500, CPT', 6, N'HLV demo phụ trách toàn bộ các lớp của Flow 2.',
             DATEADD(YEAR, -3, @Today), N'Active', @UtcNow, @UtcNow);

    DECLARE @CoachId BIGINT = (SELECT id FROM dbo.coach_profiles WHERE user_id = @CoachUserId);

    DECLARE @Members TABLE
    (
        user_id BIGINT NOT NULL,
        member_code NVARCHAR(50) NOT NULL,
        full_name NVARCHAR(150) NOT NULL,
        fitness_goal NVARCHAR(500) NOT NULL,
        fitness_level NVARCHAR(50) NOT NULL,
        date_of_birth DATE NOT NULL,
        gender NVARCHAR(20) NOT NULL,
        height_cm DECIMAL(5,2) NOT NULL,
        weight_kg DECIMAL(5,2) NOT NULL
    );

    INSERT INTO @Members
        (user_id, member_code, full_name, fitness_goal, fitness_level, date_of_birth, gender, height_cm, weight_kg)
    VALUES
        (@MemberAUserId, N'FLOW2-MEM-A', N'Nguyễn An Bình', N'Cải thiện sức bền và độ linh hoạt', N'Beginner', '1998-05-12', N'Male', 172, 68),
        (@MemberBUserId, N'FLOW2-MEM-B', N'Trần Bảo Châu', N'Giảm căng thẳng và cải thiện tư thế', N'Intermediate', '1996-09-21', N'Female', 163, 54),
        (@MemberCUserId, N'FLOW2-MEM-C', N'Lê Cường Minh', N'Tăng sức mạnh cơ lõi', N'Intermediate', '1995-03-08', N'Male', 175, 72),
        (@ExpiredUserId, N'FLOW2-MEM-X', N'Phạm Hết Hạn', N'Demo gói tập hết hạn', N'Beginner', '2000-01-15', N'Female', 160, 52);

    MERGE dbo.member_profiles WITH (HOLDLOCK) AS target
    USING @Members AS source
       ON target.user_id = source.user_id
    WHEN MATCHED THEN
        UPDATE SET
            target.center_id = @CenterId,
            target.member_code = source.member_code,
            target.full_name = source.full_name,
            target.date_of_birth = source.date_of_birth,
            target.gender = source.gender,
            target.fitness_goal = source.fitness_goal,
            target.fitness_level = source.fitness_level,
            target.medical_note = N'Không có chống chỉ định với lớp Yoga nền tảng.',
            target.height_cm = source.height_cm,
            target.weight_kg = source.weight_kg,
            target.booking_suspended_until = NULL,
            target.updated_at = @UtcNow
    WHEN NOT MATCHED THEN
        INSERT (user_id, center_id, member_code, full_name, date_of_birth, gender, fitness_goal,
                fitness_level, medical_note, height_cm, weight_kg, created_at, updated_at)
        VALUES (source.user_id, @CenterId, source.member_code, source.full_name, source.date_of_birth,
                source.gender, source.fitness_goal, source.fitness_level,
                N'Không có chống chỉ định với lớp Yoga nền tảng.', source.height_cm, source.weight_kg,
                @UtcNow, @UtcNow);

    DECLARE @MemberAId BIGINT = (SELECT id FROM dbo.member_profiles WHERE user_id = @MemberAUserId);
    DECLARE @MemberBId BIGINT = (SELECT id FROM dbo.member_profiles WHERE user_id = @MemberBUserId);
    DECLARE @MemberCId BIGINT = (SELECT id FROM dbo.member_profiles WHERE user_id = @MemberCUserId);
    DECLARE @ExpiredMemberId BIGINT = (SELECT id FROM dbo.member_profiles WHERE user_id = @ExpiredUserId);

    DECLARE @SportId BIGINT = (
        SELECT TOP (1) id
        FROM dbo.sports
        WHERE status = N'Active' AND name IN (N'Yoga & Pilates', N'FLOW2 Yoga & Pilates')
          AND (center_id IS NULL OR center_id = @CenterId)
        ORDER BY CASE WHEN center_id = @CenterId THEN 0 ELSE 1 END, id
    );
    IF @SportId IS NULL
    BEGIN
        INSERT INTO dbo.sports (center_id, name, description, status)
        VALUES (@CenterId, N'FLOW2 Yoga & Pilates', N'Bộ môn dành riêng cho kịch bản demo Flow 2.', N'Active');
        SET @SportId = SCOPE_IDENTITY();
    END;

    DECLARE @RoomAId BIGINT = (SELECT id FROM dbo.rooms WHERE center_id = @CenterId AND name = N'FLOW2 Studio A');
    IF @RoomAId IS NULL
    BEGIN
        INSERT INTO dbo.rooms (center_id, name, room_type, capacity, location_description, status, created_at)
        VALUES (@CenterId, N'FLOW2 Studio A', N'Yoga Studio', 2, N'Tầng 2 - khu demo Flow 2', N'Active', @UtcNow);
        SET @RoomAId = SCOPE_IDENTITY();
    END
    ELSE
        UPDATE dbo.rooms SET room_type = N'Yoga Studio', capacity = 2, status = N'Active' WHERE id = @RoomAId;

    DECLARE @RoomBId BIGINT = (SELECT id FROM dbo.rooms WHERE center_id = @CenterId AND name = N'FLOW2 Studio B');
    IF @RoomBId IS NULL
    BEGIN
        INSERT INTO dbo.rooms (center_id, name, room_type, capacity, location_description, status, created_at)
        VALUES (@CenterId, N'FLOW2 Studio B', N'Multi-purpose Studio', 8, N'Tầng 2 - phòng phụ demo Flow 2', N'Active', @UtcNow);
        SET @RoomBId = SCOPE_IDENTITY();
    END
    ELSE
        UPDATE dbo.rooms SET room_type = N'Multi-purpose Studio', capacity = 8, status = N'Active' WHERE id = @RoomBId;

    IF NOT EXISTS (SELECT 1 FROM dbo.coach_sports WHERE coach_id = @CoachId AND sport_id = @SportId)
        INSERT INTO dbo.coach_sports (coach_id, sport_id) VALUES (@CoachId, @SportId);

    DECLARE @PackageId BIGINT = (
        SELECT id FROM dbo.membership_packages WHERE center_id = @CenterId AND name = N'FLOW2 Demo Unlimited'
    );
    IF @PackageId IS NULL
    BEGIN
        INSERT INTO dbo.membership_packages
            (center_id, name, description, duration_days, price, max_classes, access_type,
             allowed_sports, badge, features, status, created_at, updated_at)
        VALUES
            (@CenterId, N'FLOW2 Demo Unlimited', N'Gói tập dùng cho demo booking và schedule management.',
             120, 1200000, 10, N'ClassAccess', 5, N'DEMO',
             N'["Đặt lớp trong 7 ngày","Hỗ trợ waitlist","Theo dõi điểm danh"]',
             N'Active', @UtcNow, @UtcNow);
        SET @PackageId = SCOPE_IDENTITY();
    END
    ELSE
        UPDATE dbo.membership_packages
        SET duration_days = 120, price = 1200000, max_classes = 10, access_type = N'ClassAccess',
            allowed_sports = 5, badge = N'DEMO', status = N'Active', updated_at = @UtcNow
        WHERE id = @PackageId;

    DECLARE @SubscriptionSeed TABLE
    (
        member_id BIGINT NOT NULL,
        start_date DATE NOT NULL,
        end_date DATE NOT NULL,
        status NVARCHAR(30) NOT NULL
    );
    INSERT INTO @SubscriptionSeed (member_id, start_date, end_date, status)
    VALUES
        (@MemberAId, DATEADD(DAY, -30, @Today), DATEADD(DAY, 90, @Today), N'Active'),
        (@MemberBId, DATEADD(DAY, -30, @Today), DATEADD(DAY, 90, @Today), N'Active'),
        (@MemberCId, DATEADD(DAY, -30, @Today), DATEADD(DAY, 90, @Today), N'Active'),
        (@ExpiredMemberId, DATEADD(DAY, -120, @Today), DATEADD(DAY, -1, @Today), N'Expired');

    MERGE dbo.member_subscriptions WITH (HOLDLOCK) AS target
    USING @SubscriptionSeed AS source
       ON target.member_id = source.member_id AND target.package_id = @PackageId
    WHEN MATCHED THEN
        UPDATE SET
            target.start_date = source.start_date,
            target.end_date = source.end_date,
            target.duration_days = DATEDIFF(DAY, source.start_date, source.end_date) + 1,
            target.price = 1200000,
            target.status = source.status,
            target.auto_renew = 0,
            target.updated_at = @UtcNow
    WHEN NOT MATCHED THEN
        INSERT (member_id, package_id, start_date, end_date, duration_days, price, status, auto_renew, created_at, updated_at)
        VALUES (source.member_id, @PackageId, source.start_date, source.end_date,
                DATEDIFF(DAY, source.start_date, source.end_date) + 1, 1200000,
                source.status, 0, @UtcNow, @UtcNow);

    DECLARE @SubscriptionAId BIGINT = (SELECT id FROM dbo.member_subscriptions WHERE member_id = @MemberAId AND package_id = @PackageId);
    DECLARE @SubscriptionBId BIGINT = (SELECT id FROM dbo.member_subscriptions WHERE member_id = @MemberBId AND package_id = @PackageId);
    DECLARE @SubscriptionCId BIGINT = (SELECT id FROM dbo.member_subscriptions WHERE member_id = @MemberCId AND package_id = @PackageId);

    DECLARE @ClassSeed TABLE
    (
        class_name NVARCHAR(150) NOT NULL,
        room_id BIGINT NOT NULL,
        capacity INT NOT NULL,
        allow_waitlist BIT NOT NULL,
        description NVARCHAR(1000) NOT NULL
    );
    INSERT INTO @ClassSeed (class_name, room_id, capacity, allow_waitlist, description)
    VALUES
        (N'FLOW2 - Yoga Foundation', @RoomAId, 2, 1, N'Lớp chính dùng để demo đặt chỗ, hủy chỗ và điểm danh.'),
        (N'FLOW2 - Waitlist Ready', @RoomAId, 2, 1, N'Lớp dự phòng đã đủ chỗ và có một hội viên đang chờ.'),
        (N'FLOW2 - Schedule Conflict', @RoomBId, 6, 1, N'Lớp dùng để demo lỗi hội viên đặt trùng giờ.'),
        (N'FLOW2 - Full Without Waitlist', @RoomBId, 1, 0, N'Lớp đầy và tắt waitlist để demo lỗi 409.');

    MERGE dbo.classes WITH (HOLDLOCK) AS target
    USING @ClassSeed AS source
       ON target.center_id = @CenterId AND target.name = source.class_name
    WHEN MATCHED THEN
        UPDATE SET
            target.sport_id = @SportId,
            target.room_id = source.room_id,
            target.description = source.description,
            target.level = N'Beginner',
            target.capacity = source.capacity,
            target.allow_waitlist = source.allow_waitlist,
            target.duration_minutes = 60,
            target.status = N'Published',
            target.updated_at = @UtcNow
    WHEN NOT MATCHED THEN
        INSERT (center_id, sport_id, room_id, name, description, level, capacity,
                allow_waitlist, duration_minutes, status, created_at, updated_at)
        VALUES (@CenterId, @SportId, source.room_id, source.class_name, source.description,
                N'Beginner', source.capacity, source.allow_waitlist, 60, N'Published', @UtcNow, @UtcNow);

    DECLARE @MainClassId BIGINT = (SELECT id FROM dbo.classes WHERE center_id = @CenterId AND name = N'FLOW2 - Yoga Foundation');
    DECLARE @ReadyClassId BIGINT = (SELECT id FROM dbo.classes WHERE center_id = @CenterId AND name = N'FLOW2 - Waitlist Ready');
    DECLARE @ConflictClassId BIGINT = (SELECT id FROM dbo.classes WHERE center_id = @CenterId AND name = N'FLOW2 - Schedule Conflict');
    DECLARE @NoWaitClassId BIGINT = (SELECT id FROM dbo.classes WHERE center_id = @CenterId AND name = N'FLOW2 - Full Without Waitlist');

    DECLARE @ClassIds TABLE (class_id BIGINT PRIMARY KEY);
    INSERT INTO @ClassIds VALUES (@MainClassId), (@ReadyClassId), (@ConflictClassId), (@NoWaitClassId);

    UPDATE assignment
    SET is_primary = 1, assigned_date = @Today
    FROM dbo.class_coaches AS assignment
    INNER JOIN @ClassIds AS owned ON owned.class_id = assignment.class_id
    WHERE assignment.coach_id = @CoachId;

    INSERT INTO dbo.class_coaches (class_id, coach_id, assigned_date, is_primary)
    SELECT owned.class_id, @CoachId, @Today, 1
    FROM @ClassIds AS owned
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.class_coaches
        WHERE class_id = owned.class_id AND coach_id = @CoachId
    );

    DECLARE @MainScheduleId BIGINT = (
        SELECT TOP (1) id FROM dbo.class_schedules WHERE class_id = @MainClassId ORDER BY id
    );
    IF @MainScheduleId IS NULL
    BEGIN
        INSERT INTO dbo.class_schedules
            (class_id, room_id, day_of_week, start_time, end_time, start_date, end_date, status)
        VALUES (@MainClassId, @RoomAId, @TomorrowDayOfWeek, '18:00', '19:00', @Tomorrow, @Tomorrow, N'Active');
        SET @MainScheduleId = SCOPE_IDENTITY();
    END
    ELSE
        UPDATE dbo.class_schedules
        SET room_id = @RoomAId, day_of_week = @TomorrowDayOfWeek, start_time = '18:00', end_time = '19:00',
            start_date = @Tomorrow, end_date = @Tomorrow, status = N'Active'
        WHERE id = @MainScheduleId;

    DECLARE @ReadyScheduleId BIGINT = (
        SELECT TOP (1) id FROM dbo.class_schedules WHERE class_id = @ReadyClassId ORDER BY id
    );
    IF @ReadyScheduleId IS NULL
    BEGIN
        INSERT INTO dbo.class_schedules
            (class_id, room_id, day_of_week, start_time, end_time, start_date, end_date, status)
        VALUES (@ReadyClassId, @RoomAId, @TomorrowDayOfWeek, '16:00', '17:00', @Tomorrow, @Tomorrow, N'Active');
        SET @ReadyScheduleId = SCOPE_IDENTITY();
    END
    ELSE
        UPDATE dbo.class_schedules
        SET room_id = @RoomAId, day_of_week = @TomorrowDayOfWeek, start_time = '16:00', end_time = '17:00',
            start_date = @Tomorrow, end_date = @Tomorrow, status = N'Active'
        WHERE id = @ReadyScheduleId;

    DECLARE @ConflictScheduleId BIGINT = (
        SELECT TOP (1) id FROM dbo.class_schedules WHERE class_id = @ConflictClassId ORDER BY id
    );
    IF @ConflictScheduleId IS NULL
    BEGIN
        INSERT INTO dbo.class_schedules
            (class_id, room_id, day_of_week, start_time, end_time, start_date, end_date, status)
        VALUES (@ConflictClassId, @RoomBId, @TomorrowDayOfWeek, '18:30', '19:30', @Tomorrow, @Tomorrow, N'Active');
        SET @ConflictScheduleId = SCOPE_IDENTITY();
    END
    ELSE
        UPDATE dbo.class_schedules
        SET room_id = @RoomBId, day_of_week = @TomorrowDayOfWeek, start_time = '18:30', end_time = '19:30',
            start_date = @Tomorrow, end_date = @Tomorrow, status = N'Active'
        WHERE id = @ConflictScheduleId;

    DECLARE @NoWaitScheduleId BIGINT = (
        SELECT TOP (1) id FROM dbo.class_schedules WHERE class_id = @NoWaitClassId ORDER BY id
    );
    IF @NoWaitScheduleId IS NULL
    BEGIN
        INSERT INTO dbo.class_schedules
            (class_id, room_id, day_of_week, start_time, end_time, start_date, end_date, status)
        VALUES (@NoWaitClassId, @RoomBId, @TomorrowDayOfWeek, '20:00', '21:00', @Tomorrow, @Tomorrow, N'Active');
        SET @NoWaitScheduleId = SCOPE_IDENTITY();
    END
    ELSE
        UPDATE dbo.class_schedules
        SET room_id = @RoomBId, day_of_week = @TomorrowDayOfWeek, start_time = '20:00', end_time = '21:00',
            start_date = @Tomorrow, end_date = @Tomorrow, status = N'Active'
        WHERE id = @NoWaitScheduleId;

    DECLARE @MainSessionId BIGINT = (SELECT id FROM dbo.class_sessions WHERE notes = N'FLOW2_DEMO_BK01');
    IF @MainSessionId IS NULL
    BEGIN
        INSERT INTO dbo.class_sessions
            (class_id, schedule_id, room_id, coach_id, session_date, start_time, end_time, session_status, notes, created_at)
        VALUES (@MainClassId, @MainScheduleId, @RoomAId, @CoachId, @Tomorrow, '18:00', '19:00', N'Scheduled', N'FLOW2_DEMO_BK01', @UtcNow);
        SET @MainSessionId = SCOPE_IDENTITY();
    END
    ELSE
        UPDATE dbo.class_sessions
        SET class_id = @MainClassId, schedule_id = @MainScheduleId, room_id = @RoomAId, coach_id = @CoachId,
            session_date = @Tomorrow, start_time = '18:00', end_time = '19:00', session_status = N'Scheduled'
        WHERE id = @MainSessionId;

    DECLARE @ReadySessionId BIGINT = (SELECT id FROM dbo.class_sessions WHERE notes = N'FLOW2_DEMO_WL_READY');
    IF @ReadySessionId IS NULL
    BEGIN
        INSERT INTO dbo.class_sessions
            (class_id, schedule_id, room_id, coach_id, session_date, start_time, end_time, session_status, notes, created_at)
        VALUES (@ReadyClassId, @ReadyScheduleId, @RoomAId, @CoachId, @Tomorrow, '16:00', '17:00', N'Scheduled', N'FLOW2_DEMO_WL_READY', @UtcNow);
        SET @ReadySessionId = SCOPE_IDENTITY();
    END
    ELSE
        UPDATE dbo.class_sessions
        SET class_id = @ReadyClassId, schedule_id = @ReadyScheduleId, room_id = @RoomAId, coach_id = @CoachId,
            session_date = @Tomorrow, start_time = '16:00', end_time = '17:00', session_status = N'Scheduled'
        WHERE id = @ReadySessionId;

    DECLARE @ConflictSessionId BIGINT = (SELECT id FROM dbo.class_sessions WHERE notes = N'FLOW2_DEMO_CONFLICT');
    IF @ConflictSessionId IS NULL
    BEGIN
        INSERT INTO dbo.class_sessions
            (class_id, schedule_id, room_id, coach_id, session_date, start_time, end_time, session_status, notes, created_at)
        VALUES (@ConflictClassId, @ConflictScheduleId, @RoomBId, @CoachId, @Tomorrow, '18:30', '19:30', N'Scheduled', N'FLOW2_DEMO_CONFLICT', @UtcNow);
        SET @ConflictSessionId = SCOPE_IDENTITY();
    END
    ELSE
        UPDATE dbo.class_sessions
        SET class_id = @ConflictClassId, schedule_id = @ConflictScheduleId, room_id = @RoomBId, coach_id = @CoachId,
            session_date = @Tomorrow, start_time = '18:30', end_time = '19:30', session_status = N'Scheduled'
        WHERE id = @ConflictSessionId;

    DECLARE @NoWaitSessionId BIGINT = (SELECT id FROM dbo.class_sessions WHERE notes = N'FLOW2_DEMO_NOWAIT');
    IF @NoWaitSessionId IS NULL
    BEGIN
        INSERT INTO dbo.class_sessions
            (class_id, schedule_id, room_id, coach_id, session_date, start_time, end_time, session_status, notes, created_at)
        VALUES (@NoWaitClassId, @NoWaitScheduleId, @RoomBId, @CoachId, @Tomorrow, '20:00', '21:00', N'Scheduled', N'FLOW2_DEMO_NOWAIT', @UtcNow);
        SET @NoWaitSessionId = SCOPE_IDENTITY();
    END
    ELSE
        UPDATE dbo.class_sessions
        SET class_id = @NoWaitClassId, schedule_id = @NoWaitScheduleId, room_id = @RoomBId, coach_id = @CoachId,
            session_date = @Tomorrow, start_time = '20:00', end_time = '21:00', session_status = N'Scheduled'
        WHERE id = @NoWaitSessionId;

    DECLARE @AttendanceStart TIME(7) = CAST(DATEADD(MINUTE, -15, @LocalNow) AS TIME(7));
    DECLARE @AttendanceEnd TIME(7) = CAST(DATEADD(MINUTE, 45, @LocalNow) AS TIME(7));
    DECLARE @AttendanceSessionId BIGINT = (SELECT id FROM dbo.class_sessions WHERE notes = N'FLOW2_DEMO_AT01');
    IF @AttendanceSessionId IS NULL
    BEGIN
        INSERT INTO dbo.class_sessions
            (class_id, schedule_id, room_id, coach_id, session_date, start_time, end_time, session_status, notes, created_at)
        VALUES (@MainClassId, NULL, @RoomAId, @CoachId, @Today, @AttendanceStart, @AttendanceEnd, N'Scheduled', N'FLOW2_DEMO_AT01', @UtcNow);
        SET @AttendanceSessionId = SCOPE_IDENTITY();
    END
    ELSE
        UPDATE dbo.class_sessions
        SET class_id = @MainClassId, schedule_id = NULL, room_id = @RoomAId, coach_id = @CoachId,
            session_date = @Today, start_time = @AttendanceStart, end_time = @AttendanceEnd, session_status = N'Scheduled'
        WHERE id = @AttendanceSessionId;

    DECLARE @HistorySessionId BIGINT = (SELECT id FROM dbo.class_sessions WHERE notes = N'FLOW2_DEMO_HISTORY');
    IF @HistorySessionId IS NULL
    BEGIN
        INSERT INTO dbo.class_sessions
            (class_id, schedule_id, room_id, coach_id, session_date, start_time, end_time, session_status, notes, created_at)
        VALUES (@MainClassId, NULL, @RoomAId, @CoachId, DATEADD(DAY, -1, @Today), '18:00', '19:00', N'Completed', N'FLOW2_DEMO_HISTORY', DATEADD(DAY, -1, @UtcNow));
        SET @HistorySessionId = SCOPE_IDENTITY();
    END
    ELSE
        UPDATE dbo.class_sessions
        SET class_id = @MainClassId, room_id = @RoomAId, coach_id = @CoachId,
            session_date = DATEADD(DAY, -1, @Today), start_time = '18:00', end_time = '19:00', session_status = N'Completed'
        WHERE id = @HistorySessionId;

    DECLARE @BookingSeed TABLE
    (
        session_id BIGINT NOT NULL,
        member_id BIGINT NOT NULL,
        subscription_id BIGINT NOT NULL,
        registered_by BIGINT NOT NULL,
        booked_at DATETIME2(0) NOT NULL,
        status NVARCHAR(30) NOT NULL
    );
    INSERT INTO @BookingSeed (session_id, member_id, subscription_id, registered_by, booked_at, status)
    VALUES
        (@ReadySessionId, @MemberAId, @SubscriptionAId, @MemberAUserId, DATEADD(MINUTE, -40, @UtcNow), N'Booked'),
        (@ReadySessionId, @MemberBId, @SubscriptionBId, @ReceptionUserId, DATEADD(MINUTE, -35, @UtcNow), N'Booked'),
        (@NoWaitSessionId, @MemberAId, @SubscriptionAId, @MemberAUserId, DATEADD(MINUTE, -30, @UtcNow), N'Booked'),
        (@AttendanceSessionId, @MemberAId, @SubscriptionAId, @MemberAUserId, DATEADD(HOUR, -2, @UtcNow), N'Booked'),
        (@AttendanceSessionId, @MemberBId, @SubscriptionBId, @ReceptionUserId, DATEADD(HOUR, -2, @UtcNow), N'Booked'),
        (@HistorySessionId, @MemberAId, @SubscriptionAId, @MemberAUserId, DATEADD(DAY, -2, @UtcNow), N'Booked'),
        (@HistorySessionId, @MemberBId, @SubscriptionBId, @ReceptionUserId, DATEADD(DAY, -2, @UtcNow), N'Booked');

    MERGE dbo.session_bookings WITH (HOLDLOCK) AS target
    USING @BookingSeed AS source
       ON target.session_id = source.session_id AND target.member_id = source.member_id
    WHEN MATCHED THEN
        UPDATE SET
            target.subscription_id = source.subscription_id,
            target.registered_by = source.registered_by,
            target.booked_at = source.booked_at,
            target.status = source.status
    WHEN NOT MATCHED THEN
        INSERT (session_id, member_id, subscription_id, registered_by, booked_at, status)
        VALUES (source.session_id, source.member_id, source.subscription_id, source.registered_by, source.booked_at, source.status);

    MERGE dbo.class_waitlist WITH (HOLDLOCK) AS target
    USING (SELECT @ReadySessionId AS session_id, @ReadyClassId AS class_id, @MemberCId AS member_id,
                  @SubscriptionCId AS subscription_id, @MemberCUserId AS registered_by) AS source
       ON target.session_id = source.session_id AND target.member_id = source.member_id
    WHEN MATCHED THEN
        UPDATE SET
            target.class_id = source.class_id,
            target.subscription_id = source.subscription_id,
            target.registered_by = source.registered_by,
            target.joined_at = DATEADD(MINUTE, -20, @UtcNow),
            target.status = N'Waiting'
    WHEN NOT MATCHED THEN
        INSERT (session_id, class_id, member_id, subscription_id, registered_by, joined_at, status)
        VALUES (source.session_id, source.class_id, source.member_id, source.subscription_id,
                source.registered_by, DATEADD(MINUTE, -20, @UtcNow), N'Waiting');

    MERGE dbo.attendance WITH (HOLDLOCK) AS target
    USING (
        SELECT @HistorySessionId AS session_id, @MemberAId AS member_id, N'PRESENT' AS attendance_status,
               N'Hoàn thành tốt buổi Yoga nền tảng.' AS note
        UNION ALL
        SELECT @HistorySessionId, @MemberBId, N'LATE', N'Đến muộn 8 phút nhưng hoàn thành đầy đủ bài tập.'
    ) AS source
       ON target.session_id = source.session_id AND target.member_id = source.member_id
    WHEN MATCHED THEN
        UPDATE SET
            target.checked_in_by = @CoachUserId,
            target.attendance_status = source.attendance_status,
            target.check_in_time = DATEADD(DAY, -1, @UtcNow),
            target.note = source.note
    WHEN NOT MATCHED THEN
        INSERT (session_id, member_id, checked_in_by, attendance_status, check_in_time, note, created_at)
        VALUES (source.session_id, source.member_id, @CoachUserId, source.attendance_status,
                DATEADD(DAY, -1, @UtcNow), source.note, DATEADD(DAY, -1, @UtcNow));

    DECLARE @InvoiceSeed TABLE
    (
        invoice_number NVARCHAR(50) NOT NULL,
        member_id BIGINT NOT NULL,
        transaction_code NVARCHAR(150) NOT NULL,
        idempotency_suffix NVARCHAR(20) NOT NULL
    );
    INSERT INTO @InvoiceSeed (invoice_number, member_id, transaction_code, idempotency_suffix)
    VALUES
        (N'FLOW2-INV-A', @MemberAId, N'FLOW2-PAY-A', N'a'),
        (N'FLOW2-INV-B', @MemberBId, N'FLOW2-PAY-B', N'b'),
        (N'FLOW2-INV-C', @MemberCId, N'FLOW2-PAY-C', N'c');

    MERGE dbo.invoices WITH (HOLDLOCK) AS target
    USING @InvoiceSeed AS source
       ON target.invoice_number = source.invoice_number
    WHEN MATCHED THEN
        UPDATE SET
            target.member_id = source.member_id,
            target.center_id = @CenterId,
            target.created_by = @ReceptionUserId,
            target.subtotal = 1200000,
            target.discount = 0,
            target.tax = 0,
            target.total_amount = 1200000,
            target.status = N'Paid',
            target.issued_at = @UtcNow,
            target.paid_at = @UtcNow,
            target.idempotency_key = N'flow2-demo-invoice-' + source.idempotency_suffix
    WHEN NOT MATCHED THEN
        INSERT (invoice_number, member_id, center_id, created_by, subtotal, discount, tax, total_amount,
                status, issued_at, paid_at, idempotency_key)
        VALUES (source.invoice_number, source.member_id, @CenterId, @ReceptionUserId, 1200000, 0, 0,
                1200000, N'Paid', @UtcNow, @UtcNow, N'flow2-demo-invoice-' + source.idempotency_suffix);

    MERGE dbo.payments WITH (HOLDLOCK) AS target
    USING (
        SELECT invoice.id AS invoice_id, seed.member_id, seed.transaction_code, seed.idempotency_suffix
        FROM @InvoiceSeed AS seed
        INNER JOIN dbo.invoices AS invoice ON invoice.invoice_number = seed.invoice_number
    ) AS source
       ON target.transaction_code = source.transaction_code
    WHEN MATCHED THEN
        UPDATE SET
            target.invoice_id = source.invoice_id,
            target.member_id = source.member_id,
            target.processed_by = @ReceptionUserId,
            target.payment_method = N'Cash',
            target.idempotency_key = N'flow2-demo-payment-' + source.idempotency_suffix,
            target.amount = 1200000,
            target.amount_received = 1200000,
            target.payment_status = N'Succeeded',
            target.paid_at = @UtcNow,
            target.attempted_at = @UtcNow,
            target.note = N'Thanh toán demo Flow 2',
            target.created_at = @UtcNow
    WHEN NOT MATCHED THEN
        INSERT (invoice_id, member_id, processed_by, payment_method, transaction_code, idempotency_key,
                amount, amount_received, payment_status, paid_at, created_at, attempted_at, note)
        VALUES (source.invoice_id, source.member_id, @ReceptionUserId, N'Cash', source.transaction_code,
                N'flow2-demo-payment-' + source.idempotency_suffix, 1200000, 1200000, N'Succeeded',
                @UtcNow, @UtcNow, @UtcNow, N'Thanh toán demo Flow 2');

    DECLARE @NotificationId BIGINT = (
        SELECT id FROM dbo.notifications WHERE reminder_key = N'flow2-demo-ready'
    );
    IF @NotificationId IS NULL
    BEGIN
        INSERT INTO dbo.notifications
            (sender_id, title, message, notification_type, target_class_ids, created_at, reminder_key)
        VALUES
            (@ManagerUserId, N'Dữ liệu demo Flow 2 đã sẵn sàng',
             N'Lớp, lịch, gói tập, booking, waitlist và ca điểm danh đã được chuẩn bị.',
             N'Flow2DemoReady', N'[' + CAST(@MainClassId AS NVARCHAR(30)) + N',' + CAST(@ReadyClassId AS NVARCHAR(30)) + N']',
             @UtcNow, N'flow2-demo-ready');
        SET @NotificationId = SCOPE_IDENTITY();
    END;

    DECLARE @NotificationUsers TABLE (user_id BIGINT PRIMARY KEY);
    INSERT INTO @NotificationUsers
    VALUES (@ManagerUserId), (@CoachUserId), (@ReceptionUserId), (@MemberAUserId), (@MemberBUserId), (@MemberCUserId);
    INSERT INTO dbo.user_notifications (notification_id, user_id, is_read, created_at)
    SELECT @NotificationId, target.user_id, 0, @UtcNow
    FROM @NotificationUsers AS target
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.user_notifications
        WHERE notification_id = @NotificationId AND user_id = target.user_id
    );

    DECLARE @AuditSeed TABLE
    (
        user_id BIGINT NOT NULL,
        action NVARCHAR(100) NOT NULL,
        entity_type NVARCHAR(100) NOT NULL,
        entity_id BIGINT NOT NULL,
        new_values NVARCHAR(MAX) NULL
    );
    INSERT INTO @AuditSeed (user_id, action, entity_type, entity_id, new_values)
    VALUES
        (@ManagerUserId, N'class.created', N'Class', @MainClassId, N'{"source":"FLOW2_DEMO"}'),
        (@ManagerUserId, N'CLASS_COACH.ASSIGNED', N'Class', @MainClassId, N'{"coach":"flow2.coach"}'),
        (@ManagerUserId, N'class_schedule.created', N'ClassSchedule', @MainScheduleId, N'{"source":"FLOW2_DEMO"}'),
        (@ManagerUserId, N'class.published', N'Class', @MainClassId, N'{"status":"Published"}'),
        (@MemberAUserId, N'BOOK_SESSION', N'ClassSession', @ReadySessionId, N'{"status":"Booked"}'),
        (@ReceptionUserId, N'BOOK_SESSION', N'ClassSession', @ReadySessionId, N'{"member":"FLOW2-MEM-B"}'),
        (@MemberCUserId, N'JOIN_SESSION_WAITLIST', N'ClassSession', @ReadySessionId, N'{"status":"Waiting"}');

    INSERT INTO dbo.audit_logs
        (user_id, center_id, action, entity_type, entity_id, new_values, ip_address, user_agent, created_at)
    SELECT seed.user_id, @CenterId, seed.action, seed.entity_type, seed.entity_id, seed.new_values,
           N'127.0.0.1', N'FLOW2 Demo Seeder', @UtcNow
    FROM @AuditSeed AS seed
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.audit_logs AS existing
        WHERE existing.user_id = seed.user_id
          AND existing.action = seed.action
          AND existing.entity_type = seed.entity_type
          AND existing.entity_id = seed.entity_id
          AND existing.user_agent = N'FLOW2 Demo Seeder'
    );

    COMMIT TRANSACTION;

    SELECT
        N'FLOW2_DEMO_READY' AS result,
        @CenterId AS center_id,
        @Today AS demo_date,
        @Tomorrow AS booking_date,
        7 AS account_count,
        4 AS class_count,
        6 AS session_count,
        3600000 AS seeded_revenue;

    SELECT
        account.username,
        account.email,
        role.name AS role,
        N'Member@123456' AS demo_password
    FROM dbo.users AS account
    INNER JOIN dbo.roles AS role ON role.id = account.role_id
    WHERE account.username LIKE N'flow2.%'
    ORDER BY account.username;

    SELECT
        session.id AS session_id,
        session.notes AS scenario_code,
        class.name AS class_name,
        session.session_date,
        session.start_time,
        session.end_time,
        session.session_status,
        (SELECT COUNT(*) FROM dbo.session_bookings AS booking
         WHERE booking.session_id = session.id AND booking.status = N'Booked') AS booked,
        (SELECT COUNT(*) FROM dbo.class_waitlist AS waiting
         WHERE waiting.session_id = session.id AND waiting.status = N'Waiting') AS waiting
    FROM dbo.class_sessions AS session
    INNER JOIN dbo.classes AS class ON class.id = session.class_id
    WHERE session.notes LIKE N'FLOW2_DEMO_%'
    ORDER BY session.session_date, session.start_time;

    SELECT
        member.member_code,
        member.full_name,
        subscription.status AS subscription_status,
        subscription.start_date,
        subscription.end_date
    FROM dbo.member_profiles AS member
    INNER JOIN dbo.member_subscriptions AS subscription ON subscription.member_id = member.id
    WHERE member.member_code LIKE N'FLOW2-MEM-%'
    ORDER BY member.member_code;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;


GO

-- =========================================================================================
-- PHẦN 3: DỮ LIỆU MẪU HUẤN LUYỆN VIÊN (COACH FULL TEST DATA: GIÁO ÁN, CHẤM CÔNG, KPI, LỊCH TRỰC)
-- =========================================================================================

-- =========================================================================================
-- SEED FULL TEST DATA FOR ACTOR COACH (SCMS - SPORTS CENTER MANAGEMENT SYSTEM)
-- Covers all 8 functional groups:
-- 1. Teaching Schedule, Sessions, Substitute Coach, Rosters
-- 2. Member Profiles, Goals, Medical & Health Records (Hypertension, Spine, Knee, Rotator Cuff)
-- 3. Training Plans (Class & Personal), Exercises, Sets, Reps, Intensity
-- 4. Session Results & Post-Workout Logging
-- 5. Body Metrics Progress Reviews & History Tracking (Weight, BodyFat, MuscleMass, BMI)
-- 6. Live Attendance (Present, Absent, Late with notes, Excused)
-- 7. Homework Assignments, Submissions (Submitted & Reviewed with grades/feedback)
-- 8. Notifications, Class Broadcasts & Coach-Member Direct Chat
-- 9. Coach Calendar Events (1-on-1 PT, Shift, Workshop), Leave Requests & Swap Coach
-- 10. Coach Compensation Policy & Sales Commission (Salary & KPI)
-- =========================================================================================

SET NOCOUNT ON;
BEGIN TRANSACTION;

BEGIN TRY

    DECLARE @CenterId BIGINT = 1;
    DECLARE @Coach1UserId BIGINT = 10;
    DECLARE @Coach2UserId BIGINT = 11;
    DECLARE @ManagerUserId BIGINT = 9;
    DECLARE @AdminUserId BIGINT = 2;
    DECLARE @MemberRole BIGINT = 1;

    -- Password hash for Member@123456
    DECLARE @MemberPasswordHash NVARCHAR(255) = N'$2a$11$1k4jiQRrxN6VjiS3pvnSXuS4hCQMsackbN6sTYXVo46936W7DyZqe';

    -- -----------------------------------------------------------------------------------------
    -- 1. UPDATE COACH PROFILES & SKILLS
    -- -----------------------------------------------------------------------------------------
    UPDATE coach_profiles
    SET full_name = N'Phạm Quang Huy',
        specialization = N'Fitness & Strength Conditioning, Kháng lực & Giảm mỡ',
        bio = N'HLV Trưởng với 8 năm kinh nghiệm đào tạo thể hình, huấn luyện sức mạnh và phục hồi vận động. Chứng chỉ NASM-CPT, CSCS.',
        experience_years = 8,
        hire_date = '2022-03-15',
        status = N'Active',
        updated_at = SYSUTCDATETIME()
    WHERE user_id = @Coach1UserId;

    UPDATE coach_profiles
    SET full_name = N'Đặng Minh Tuấn',
        specialization = N'Yoga, Pilates & Phục hồi chức năng vận động',
        bio = N'Chuyên gia Yoga & Pilates trị liệu, điều trị đau lưng, vẹo cột sống và hồi phục cơ sau chấn thương.',
        experience_years = 5,
        hire_date = '2023-06-01',
        status = N'Active',
        updated_at = SYSUTCDATETIME()
    WHERE user_id = @Coach2UserId;

    DECLARE @Coach1Id BIGINT = (SELECT id FROM coach_profiles WHERE user_id = @Coach1UserId);
    DECLARE @Coach2Id BIGINT = (SELECT id FROM coach_profiles WHERE user_id = @Coach2UserId);

    -- Map Coach Sports
    IF NOT EXISTS (SELECT 1 FROM coach_sports WHERE coach_id = @Coach1Id AND sport_id = 3)
        INSERT INTO coach_sports (coach_id, sport_id) VALUES (@Coach1Id, 3); -- Gym
    IF NOT EXISTS (SELECT 1 FROM coach_sports WHERE coach_id = @Coach1Id AND sport_id = 8)
        INSERT INTO coach_sports (coach_id, sport_id) VALUES (@Coach1Id, 8); -- Boxing
    IF NOT EXISTS (SELECT 1 FROM coach_sports WHERE coach_id = @Coach2Id AND sport_id = 6)
        INSERT INTO coach_sports (coach_id, sport_id) VALUES (@Coach2Id, 6); -- Yoga
    IF NOT EXISTS (SELECT 1 FROM coach_sports WHERE coach_id = @Coach2Id AND sport_id = 3)
        INSERT INTO coach_sports (coach_id, sport_id) VALUES (@Coach2Id, 3); -- Gym

    -- -----------------------------------------------------------------------------------------
    -- 2. UPDATE EXISTING MEMBERS & ADD REALISTIC NEW MEMBERS
    -- -----------------------------------------------------------------------------------------
    -- Member 01 (UserId 4): Nguyễn Văn An (Cao huyết áp)
    UPDATE member_profiles
    SET full_name = N'Nguyễn Văn An',
        fitness_goal = N'Giảm mỡ & Kiểm soát huyết áp',
        fitness_level = N'Beginner',
        gender = N'Male',
        date_of_birth = '1992-05-14',
        height_cm = 172.0,
        weight_kg = 84.0,
        medical_note = N'Tiền sử cao huyết áp nhẹ (135/85 mmHg)',
        emergency_contact_name = N'Nguyễn Thị Thu (Vợ)',
        emergency_contact_phone = N'0908123456',
        center_id = @CenterId,
        updated_at = SYSUTCDATETIME()
    WHERE user_id = 4;

    -- Member 02 (UserId 5): Trần Thị Mai (Thoát vị đĩa đệm L4-L5)
    UPDATE member_profiles
    SET full_name = N'Trần Thị Mai',
        fitness_goal = N'Phục hồi cột sống thắt lưng & Săn chắc cơ',
        fitness_level = N'Intermediate',
        gender = N'Female',
        date_of_birth = '1996-08-22',
        height_cm = 162.0,
        weight_kg = 54.0,
        medical_note = N'Thoát vị đĩa đệm L4-L5, đau thắt lưng khi gập sâu',
        emergency_contact_name = N'Trần Văn Bình (Bố)',
        emergency_contact_phone = N'0918765432',
        center_id = @CenterId,
        updated_at = SYSUTCDATETIME()
    WHERE user_id = 5;

    -- Member 03: Lê Hoàng Nam (Tập Sức mạnh / Hypertrophy)
    IF NOT EXISTS (SELECT 1 FROM users WHERE username = N'member03')
    BEGIN
        INSERT INTO users (username, email, password_hash, phone, role_id, status, auth_version, created_at, updated_at)
        VALUES (N'member03', N'member03@sportscenter.vn', @MemberPasswordHash, N'0912345603', @MemberRole, N'Active', 0, SYSUTCDATETIME(), SYSUTCDATETIME());

        DECLARE @Member03UserId BIGINT = SCOPE_IDENTITY();
        INSERT INTO member_profiles (user_id, member_code, full_name, date_of_birth, gender, address, emergency_contact_name, emergency_contact_phone, fitness_goal, fitness_level, medical_note, height_cm, weight_kg, center_id, created_at, updated_at)
        VALUES (@Member03UserId, N'MEM-2026-003', N'Lê Hoàng Nam', '1998-11-03', N'Male', N'72 Lê Thánh Tôn, Q1, TP.HCM', N'Lê Văn Hùng', N'0903334455', N'Tăng cơ xẻ nét & Sức mạnh tối đa', N'Advanced', N'Khỏe mạnh, không có bệnh lý nền', 178.0, 76.0, @CenterId, SYSUTCDATETIME(), SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        UPDATE member_profiles
        SET full_name = N'Lê Hoàng Nam',
            fitness_goal = N'Tăng cơ xẻ nét & Sức mạnh tối đa',
            fitness_level = N'Advanced',
            medical_note = N'Khỏe mạnh, không có bệnh lý nền',
            gender = N'Male',
            date_of_birth = '1998-11-03',
            height_cm = 178.0,
            weight_kg = 76.0,
            center_id = @CenterId,
            updated_at = SYSUTCDATETIME()
        WHERE user_id = (SELECT id FROM users WHERE username = N'member03');
    END

    -- Member 04: Phạm Thùy Linh (Chấn thương khớp gối)
    IF NOT EXISTS (SELECT 1 FROM users WHERE username = N'member04')
    BEGIN
        INSERT INTO users (username, email, password_hash, phone, role_id, status, auth_version, created_at, updated_at)
        VALUES (N'member04', N'member04@sportscenter.vn', @MemberPasswordHash, N'0912345604', @MemberRole, N'Active', 0, SYSUTCDATETIME(), SYSUTCDATETIME());

        DECLARE @Member04UserId BIGINT = SCOPE_IDENTITY();
        INSERT INTO member_profiles (user_id, member_code, full_name, date_of_birth, gender, address, emergency_contact_name, emergency_contact_phone, fitness_goal, fitness_level, medical_note, height_cm, weight_kg, center_id, created_at, updated_at)
        VALUES (@Member04UserId, N'MEM-2026-004', N'Phạm Thùy Linh', '2000-02-18', N'Female', N'120 Hai Bà Trưng, Q1, TP.HCM', N'Nguyễn Kim Dung', N'0912998877', N'Yoga dẻo dai & Hồi phục sụn gối', N'Beginner', N'Rách sụn chêm đầu gối trái nhẹ, tránh bật nhảy', 165.0, 52.0, @CenterId, SYSUTCDATETIME(), SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        UPDATE member_profiles
        SET full_name = N'Phạm Thùy Linh',
            fitness_goal = N'Yoga dẻo dai & Hồi phục sụn gối',
            fitness_level = N'Beginner',
            medical_note = N'Rách sụn chêm đầu gối trái nhẹ, tránh bật nhảy',
            gender = N'Female',
            date_of_birth = '2000-02-18',
            height_cm = 165.0,
            weight_kg = 52.0,
            center_id = @CenterId,
            updated_at = SYSUTCDATETIME()
        WHERE user_id = (SELECT id FROM users WHERE username = N'member04');
    END

    -- Member 05: Võ Minh Đức (Đau cơ chóp xoay vai / Golf)
    IF NOT EXISTS (SELECT 1 FROM users WHERE username = N'member05')
    BEGIN
        INSERT INTO users (username, email, password_hash, phone, role_id, status, auth_version, created_at, updated_at)
        VALUES (N'member05', N'member05@sportscenter.vn', @MemberPasswordHash, N'0912345605', @MemberRole, N'Active', 0, SYSUTCDATETIME(), SYSUTCDATETIME());

        DECLARE @Member05UserId BIGINT = SCOPE_IDENTITY();
        INSERT INTO member_profiles (user_id, member_code, full_name, date_of_birth, gender, address, emergency_contact_name, emergency_contact_phone, fitness_goal, fitness_level, medical_note, height_cm, weight_kg, center_id, created_at, updated_at)
        VALUES (@Member05UserId, N'MEM-2026-005', N'Võ Minh Đức', '1990-09-10', N'Male', N'35 Nguyễn Huệ, Q1, TP.HCM', N'Võ Thị Ngọc', N'0988776655', N'Golf chuyên nghiệp & Sức mạnh trục xoay', N'Intermediate', N'Đau nhẹ cơ chóp xoay vai phải khi vung tay quá đà', 175.0, 72.0, @CenterId, SYSUTCDATETIME(), SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        UPDATE member_profiles
        SET full_name = N'Võ Minh Đức',
            fitness_goal = N'Golf chuyên nghiệp & Sức mạnh trục xoay',
            fitness_level = N'Intermediate',
            medical_note = N'Đau nhẹ cơ chóp xoay vai phải khi vung tay quá đà',
            gender = N'Male',
            date_of_birth = '1990-09-10',
            height_cm = 175.0,
            weight_kg = 72.0,
            center_id = @CenterId,
            updated_at = SYSUTCDATETIME()
        WHERE user_id = (SELECT id FROM users WHERE username = N'member05');
    END

    DECLARE @Member1Id BIGINT = (SELECT id FROM member_profiles WHERE user_id = 4); -- An
    DECLARE @Member2Id BIGINT = (SELECT id FROM member_profiles WHERE user_id = 5); -- Mai
    DECLARE @Member3Id BIGINT = (SELECT id FROM member_profiles WHERE user_id = (SELECT id FROM users WHERE username = N'member03')); -- Nam
    DECLARE @Member4Id BIGINT = (SELECT id FROM member_profiles WHERE user_id = (SELECT id FROM users WHERE username = N'member04')); -- Linh
    DECLARE @Member5Id BIGINT = (SELECT id FROM member_profiles WHERE user_id = (SELECT id FROM users WHERE username = N'member05')); -- Đức

    -- -----------------------------------------------------------------------------------------
    -- 3. MEDICAL & HEALTH RECORDS (FOR COACH MEDICAL NLP & AI WORKOUT SAFETY)
    -- -----------------------------------------------------------------------------------------
    -- Clear and recreate health records for clean demonstration
    DELETE FROM member_health_records WHERE member_id IN (@Member1Id, @Member2Id, @Member3Id, @Member4Id, @Member5Id);

    INSERT INTO member_health_records (member_id, coach_id, record_type, title, details, occurred_on, status, created_at)
    VALUES
    (@Member1Id, @Coach1Id, N'Condition', N'Tiền sử cao huyết áp độ 1 & Nhịp tim nhanh',
     N'Hội viên có tiền sử cao huyết áp (135/85 mmHg), nhịp tim nhanh khi vận động cường độ cao. Cấm bài tập nín thở (Valsalva), hạn chế bài tập HIIT đẩy nhịp tim vượt quá 150 bpm.', '2026-01-10', N'Active', SYSUTCDATETIME()),

    (@Member2Id, @Coach1Id, N'Injury', N'Thoát vị đĩa đệm L4-L5 thắt lưng',
     N'Thoát vị đĩa đệm L4-L5, đau thắt lưng khi gập sâu hoặc gánh tạ nén dọc trục cột sống. Cấm hoàn toàn Barbell Back Squat, Deadlift nặng, Overhead Press. Khuyến nghị bài tập giãn cơ và tăng cường cơ lõi tựa lưng.', '2026-03-20', N'Active', SYSUTCDATETIME()),

    (@Member4Id, @Coach2Id, N'Injury', N'Rách sụn chêm đầu gối trái (Meniscus Tear)',
     N'Tổn thương sụn chêm khớp gối trái. Tránh các bài tập gập gối quá 90 độ, nhảy bục (box jump), lunge bước dài hoặc chạy nước rút (sprint). Ưu tiên bơi lội, đạp xe nhẹ và bài tập phục hồi đẳng trường.', '2026-05-15', N'Active', SYSUTCDATETIME()),

    (@Member5Id, @Coach1Id, N'Limitation', N'Căng cơ chóp xoay vai phải (Rotator Cuff Strain)',
     N'Căng cơ dưới gai và cơ tròn bé vai phải. Đau khi nâng tạ qua đầu hoặc xoay ngoài biên độ rộng. Cần khởi động kỹ khớp vai trước khi tập swing.', '2026-07-01', N'Active', SYSUTCDATETIME());

    -- -----------------------------------------------------------------------------------------
    -- 4. CLASSES & ASSIGNMENTS TO COACHES
    -- -----------------------------------------------------------------------------------------
    UPDATE classes
    SET name = N'Gym Kháng Lực & Tăng Cơ Toàn Diện',
        description = N'Lớp rèn luyện sức mạnh, tăng cơ bắp và giảm mỡ với giáo án khoa học chuẩn thể hình.',
        level = N'All Levels',
        room_id = 4, -- High Performance Gym
        capacity = 25,
        status = N'Published'
    WHERE id = 3;

    UPDATE classes
    SET name = N'Golf Kỹ Thuật Swing TrackMan 3D',
        description = N'Phân tích chuyển động Swing 3D TrackMan, cải thiện lực đánh và độ chính xác.',
        level = N'Intermediate',
        room_id = 2, -- Golf 3D
        capacity = 15,
        status = N'Published'
    WHERE id = 4;

    -- Create/Update Class 5: Yoga & Phục Hồi (Coach 2 primary)
    IF NOT EXISTS (SELECT 1 FROM classes WHERE id = 5)
    BEGIN
        SET IDENTITY_INSERT classes ON;
        INSERT INTO classes (id, center_id, sport_id, room_id, name, description, level, capacity, duration_minutes, status, allow_waitlist, created_at, updated_at)
        VALUES (5, @CenterId, 6, 5, N'Yoga Vinyasa & Phục Hồi Cột Sống', N'Kéo giãn cột sống, giải tỏa đau mỏi thắt lưng và hồi phục cơ bắp sau tuần làm việc.', N'Beginner', 20, 60, N'Published', 1, SYSUTCDATETIME(), SYSUTCDATETIME());
        SET IDENTITY_INSERT classes OFF;
    END
    ELSE
    BEGIN
        UPDATE classes
        SET name = N'Yoga Vinyasa & Phục Hồi Cột Sống',
            description = N'Kéo giãn cột sống, giải tỏa đau mỏi thắt lưng và hồi phục cơ bắp sau tuần làm việc.',
            level = N'Beginner',
            status = N'Published',
            updated_at = SYSUTCDATETIME()
        WHERE id = 5;
    END

    -- Create/Update Class 6: HIIT & Cardio Đốt Mỡ (Coach 1 primary)
    IF NOT EXISTS (SELECT 1 FROM classes WHERE id = 6)
    BEGIN
        SET IDENTITY_INSERT classes ON;
        INSERT INTO classes (id, center_id, sport_id, room_id, name, description, level, capacity, duration_minutes, status, allow_waitlist, created_at, updated_at)
        VALUES (6, @CenterId, 3, 1, N'HIIT & Cardio Đốt Mỡ Chuyên Sâu', N'Tập luyện chuỗi bài tập cường độ cao ngắt quãng, tối ưu đốt cháy calo và tăng thể lực.', N'Intermediate', 20, 45, N'Published', 1, SYSUTCDATETIME(), SYSUTCDATETIME());
        SET IDENTITY_INSERT classes OFF;
    END
    ELSE
    BEGIN
        UPDATE classes
        SET name = N'HIIT & Cardio Đốt Mỡ Chuyên Sâu',
            description = N'Tập luyện chuỗi bài tập cường độ cao ngắt quãng, tối ưu đốt cháy calo và tăng thể lực.',
            level = N'Intermediate',
            status = N'Published',
            updated_at = SYSUTCDATETIME()
        WHERE id = 6;
    END

    -- Class Coaches
    DELETE FROM class_coaches WHERE class_id IN (3, 4, 5, 6);
    INSERT INTO class_coaches (class_id, coach_id, assigned_date, is_primary) VALUES (3, @Coach1Id, '2026-09-01', 1);
    INSERT INTO class_coaches (class_id, coach_id, assigned_date, is_primary) VALUES (4, @Coach1Id, '2026-09-01', 1);
    INSERT INTO class_coaches (class_id, coach_id, assigned_date, is_primary) VALUES (5, @Coach2Id, '2026-09-01', 1);
    INSERT INTO class_coaches (class_id, coach_id, assigned_date, is_primary) VALUES (6, @Coach1Id, '2026-09-01', 1);

    -- -----------------------------------------------------------------------------------------
    -- 5. CLASS ENROLLMENTS
    -- -----------------------------------------------------------------------------------------
    -- Class 3: An, Nam, Đức
    IF NOT EXISTS (SELECT 1 FROM class_enrollments WHERE class_id = 3 AND member_id = @Member1Id)
        INSERT INTO class_enrollments (class_id, member_id, status, registered_at) VALUES (3, @Member1Id, N'Active', SYSUTCDATETIME());
    IF NOT EXISTS (SELECT 1 FROM class_enrollments WHERE class_id = 3 AND member_id = @Member3Id)
        INSERT INTO class_enrollments (class_id, member_id, status, registered_at) VALUES (3, @Member3Id, N'Active', SYSUTCDATETIME());
    IF NOT EXISTS (SELECT 1 FROM class_enrollments WHERE class_id = 3 AND member_id = @Member5Id)
        INSERT INTO class_enrollments (class_id, member_id, status, registered_at) VALUES (3, @Member5Id, N'Active', SYSUTCDATETIME());
    -- Also enroll Mai so Coach can test roster with enrolled members
    IF NOT EXISTS (SELECT 1 FROM class_enrollments WHERE class_id = 3 AND member_id = @Member2Id)
        INSERT INTO class_enrollments (class_id, member_id, status, registered_at) VALUES (3, @Member2Id, N'Active', SYSUTCDATETIME());

    -- Class 4: Đức, An
    IF NOT EXISTS (SELECT 1 FROM class_enrollments WHERE class_id = 4 AND member_id = @Member5Id)
        INSERT INTO class_enrollments (class_id, member_id, status, registered_at) VALUES (4, @Member5Id, N'Active', SYSUTCDATETIME());
    IF NOT EXISTS (SELECT 1 FROM class_enrollments WHERE class_id = 4 AND member_id = @Member1Id)
        INSERT INTO class_enrollments (class_id, member_id, status, registered_at) VALUES (4, @Member1Id, N'Active', SYSUTCDATETIME());

    -- Class 5: Mai, Linh
    IF NOT EXISTS (SELECT 1 FROM class_enrollments WHERE class_id = 5 AND member_id = @Member2Id)
        INSERT INTO class_enrollments (class_id, member_id, status, registered_at) VALUES (5, @Member2Id, N'Active', SYSUTCDATETIME());
    IF NOT EXISTS (SELECT 1 FROM class_enrollments WHERE class_id = 5 AND member_id = @Member4Id)
        INSERT INTO class_enrollments (class_id, member_id, status, registered_at) VALUES (5, @Member4Id, N'Active', SYSUTCDATETIME());

    -- Class 6: An, Nam
    IF NOT EXISTS (SELECT 1 FROM class_enrollments WHERE class_id = 6 AND member_id = @Member1Id)
        INSERT INTO class_enrollments (class_id, member_id, status, registered_at) VALUES (6, @Member1Id, N'Active', SYSUTCDATETIME());
    IF NOT EXISTS (SELECT 1 FROM class_enrollments WHERE class_id = 6 AND member_id = @Member3Id)
        INSERT INTO class_enrollments (class_id, member_id, status, registered_at) VALUES (6, @Member3Id, N'Active', SYSUTCDATETIME());

    -- -----------------------------------------------------------------------------------------
    -- 6. CLASS SESSIONS (TIMELINE: TODAY LIVE, TODAY PAST, SUBSTITUTE, OVERNIGHT, UPCOMING)
    -- -----------------------------------------------------------------------------------------
    -- Clear previous test sessions and their bookings/attendances for classes 3, 4, 5, 6
    DELETE FROM session_bookings WHERE session_id IN (SELECT id FROM class_sessions WHERE class_id IN (3, 4, 5, 6));
    DELETE FROM attendance WHERE session_id IN (SELECT id FROM class_sessions WHERE class_id IN (3, 4, 5, 6));
    DELETE FROM training_results WHERE session_id IN (SELECT id FROM class_sessions WHERE class_id IN (3, 4, 5, 6));
    DELETE FROM class_sessions WHERE class_id IN (3, 4, 5, 6);

    -- SESSION 101: ĐANG DIỄN RA TỐI NAY (20:00 - 22:30, Date: 2026-10-25) -> READY FOR LIVE ATTENDANCE!
    -- Current local time is ~21:50, so this session is active right now!
    INSERT INTO class_sessions (class_id, room_id, coach_id, session_date, start_time, end_time, session_status, notes, created_at)
    VALUES (3, 4, @Coach1Id, '2026-10-25', '20:00:00', '22:30:00', N'Scheduled', N'Buổi tập Hypertrophy Ngực & Tay sau - Thực hành kỹ thuật Bench Press', SYSUTCDATETIME());
    DECLARE @LiveSessionId BIGINT = SCOPE_IDENTITY();

    -- SESSION 102: VỪA KẾT THÚC CHIỀU NAY (16:00 - 17:30, Date: 2026-10-25) -> READY FOR WORKOUT LOGGING!
    INSERT INTO class_sessions (class_id, room_id, coach_id, session_date, start_time, end_time, session_status, notes, created_at)
    VALUES (6, 1, @Coach1Id, '2026-10-25', '16:00:00', '17:30:00', N'Scheduled', N'Buổi Cardio & Thể lực sức bền nhóm cơ chân', SYSUTCDATETIME());
    DECLARE @PastSessionTodayId BIGINT = SCOPE_IDENTITY();

    -- SESSION 103: BUỔI DẠY THAY (Substitute Coach, Date: 2026-10-26 08:00 - 09:30) -> COACH-B01
    -- Class 5 is normally Coach 2, but session is assigned specifically to Coach 1 (Coach1Id)
    INSERT INTO class_sessions (class_id, room_id, coach_id, session_date, start_time, end_time, session_status, notes, created_at)
    VALUES (5, 5, @Coach1Id, '2026-10-26', '08:00:00', '09:30:00', N'Scheduled', N'Ca dạy thay HLV Tuấn: Yoga Vinyasa giãn cơ toàn thân', SYSUTCDATETIME());
    DECLARE @SubstituteSessionId BIGINT = SCOPE_IDENTITY();

    -- SESSION 104: BUỔI TẬP QUA ĐÊM (Overnight Session: 23:00 - 01:00, Date: 2026-10-26) -> COACH-B04
    INSERT INTO class_sessions (class_id, room_id, coach_id, session_date, start_time, end_time, session_status, notes, created_at)
    VALUES (3, 4, @Coach1Id, '2026-10-26', '23:00:00', '01:00:00', N'Scheduled', N'Ca tập muộn Night Owl Fitness - Sức mạnh kháng lực', SYSUTCDATETIME());

    -- UPCOMING SESSIONS IN WEEK
    INSERT INTO class_sessions (class_id, room_id, coach_id, session_date, start_time, end_time, session_status, notes, created_at)
    VALUES
    (3, 4, @Coach1Id, '2026-10-27', '08:30:00', '10:00:00', N'Scheduled', N'Chân & Mông: Kỹ thuật Squat và Hip Thrust', SYSUTCDATETIME()),
    (4, 2, @Coach1Id, '2026-10-28', '14:00:00', '15:30:00', N'Scheduled', N'Chỉnh sửa Swing TrackMan: Góc tấn công (Angle of Attack)', SYSUTCDATETIME()),
    (3, 4, @Coach1Id, '2026-10-29', '18:00:00', '19:30:00', N'Scheduled', N'Lưng xô & Tay trước: Deadlift và Pull-up', SYSUTCDATETIME()),
    (6, 1, @Coach1Id, '2026-10-30', '09:00:00', '10:30:00', N'Scheduled', N'Tabata & Core Conditioning', SYSUTCDATETIME()),
    (3, 4, @Coach1Id, '2026-10-31', '10:00:00', '11:30:00', N'Scheduled', N'Tổng hợp thể lực cuối tuần', SYSUTCDATETIME());

    -- PAST SESSIONS IN PREVIOUS DAYS
    INSERT INTO class_sessions (class_id, room_id, coach_id, session_date, start_time, end_time, session_status, notes, created_at)
    VALUES
    (3, 4, @Coach1Id, '2026-10-22', '08:00:00', '09:30:00', N'Completed', N'Kháng lực chu kỳ 1: Thích nghi cơ bắp', SYSUTCDATETIME()),
    (3, 4, @Coach1Id, '2026-10-24', '08:00:00', '09:30:00', N'Completed', N'Kháng lực chu kỳ 2: Tăng tải trọng lượng', SYSUTCDATETIME());
    DECLARE @PastSession1Id BIGINT = (SELECT id FROM class_sessions WHERE session_date = '2026-10-22' AND class_id = 3);
    DECLARE @PastSession2Id BIGINT = (SELECT id FROM class_sessions WHERE session_date = '2026-10-24' AND class_id = 3);

    -- -----------------------------------------------------------------------------------------
    -- 7. SESSION BOOKINGS & ATTENDANCES
    -- -----------------------------------------------------------------------------------------
    -- Live Session (101): Bookings for An, Nam, Đức
    INSERT INTO session_bookings (session_id, member_id, booked_at, status) VALUES (@LiveSessionId, @Member1Id, SYSUTCDATETIME(), N'Booked');
    INSERT INTO session_bookings (session_id, member_id, booked_at, status) VALUES (@LiveSessionId, @Member3Id, SYSUTCDATETIME(), N'Booked');
    INSERT INTO session_bookings (session_id, member_id, booked_at, status) VALUES (@LiveSessionId, @Member5Id, SYSUTCDATETIME(), N'Booked');

    -- Live Session Attendance: An (Present), Nam (Present), Đức (Late with Note), Mai (Enrolled, Not yet attended)
    INSERT INTO attendance (session_id, member_id, checked_in_by, attendance_status, check_in_time, note, created_at)
    VALUES (@LiveSessionId, @Member1Id, @Coach1UserId, N'PRESENT', SYSUTCDATETIME(), N'Có mặt đúng giờ, thể trạng tốt', SYSUTCDATETIME());

    INSERT INTO attendance (session_id, member_id, checked_in_by, attendance_status, check_in_time, note, created_at)
    VALUES (@LiveSessionId, @Member3Id, @Coach1UserId, N'PRESENT', SYSUTCDATETIME(), N'Khởi động đầy đủ', SYSUTCDATETIME());

    INSERT INTO attendance (session_id, member_id, checked_in_by, attendance_status, check_in_time, note, created_at)
    VALUES (@LiveSessionId, @Member5Id, @Coach1UserId, N'LATE', SYSUTCDATETIME(), N'Đi muộn 15 phút do kẹt xe, đã khởi động bù khớp vai', SYSUTCDATETIME());

    -- Past Session Today (102): An & Nam Booked and Attended
    INSERT INTO session_bookings (session_id, member_id, booked_at, status) VALUES (@PastSessionTodayId, @Member1Id, SYSUTCDATETIME(), N'Booked');
    INSERT INTO session_bookings (session_id, member_id, booked_at, status) VALUES (@PastSessionTodayId, @Member3Id, SYSUTCDATETIME(), N'Booked');
    INSERT INTO attendance (session_id, member_id, checked_in_by, attendance_status, check_in_time, note, created_at)
    VALUES (@PastSessionTodayId, @Member1Id, @Coach1UserId, N'PRESENT', SYSUTCDATETIME(), N'Hoàn thành đầy đủ các hiệp chạy', SYSUTCDATETIME());
    INSERT INTO attendance (session_id, member_id, checked_in_by, attendance_status, check_in_time, note, created_at)
    VALUES (@PastSessionTodayId, @Member3Id, @Coach1UserId, N'PRESENT', SYSUTCDATETIME(), N'Đạt nhịp tim tối đa 165 bpm', SYSUTCDATETIME());

    -- Substitute Session (103): Mai & Linh Booked
    INSERT INTO session_bookings (session_id, member_id, booked_at, status) VALUES (@SubstituteSessionId, @Member2Id, SYSUTCDATETIME(), N'Booked');
    INSERT INTO session_bookings (session_id, member_id, booked_at, status) VALUES (@SubstituteSessionId, @Member4Id, SYSUTCDATETIME(), N'Booked');

    -- Past Sessions (Oct 22 & Oct 24): Full bookings and attendances
    INSERT INTO session_bookings (session_id, member_id, booked_at, status) VALUES (@PastSession1Id, @Member1Id, SYSUTCDATETIME(), N'Booked');
    INSERT INTO session_bookings (session_id, member_id, booked_at, status) VALUES (@PastSession1Id, @Member3Id, SYSUTCDATETIME(), N'Booked');
    INSERT INTO attendance (session_id, member_id, checked_in_by, attendance_status, check_in_time, note, created_at)
    VALUES (@PastSession1Id, @Member1Id, @Coach1UserId, N'PRESENT', SYSUTCDATETIME(), N'Đạt chỉ tiêu buổi tập', SYSUTCDATETIME());
    INSERT INTO attendance (session_id, member_id, checked_in_by, attendance_status, check_in_time, note, created_at)
    VALUES (@PastSession1Id, @Member3Id, @Coach1UserId, N'PRESENT', SYSUTCDATETIME(), N'Đạt chỉ tiêu buổi tập', SYSUTCDATETIME());

    INSERT INTO session_bookings (session_id, member_id, booked_at, status) VALUES (@PastSession2Id, @Member1Id, SYSUTCDATETIME(), N'Booked');
    INSERT INTO session_bookings (session_id, member_id, booked_at, status) VALUES (@PastSession2Id, @Member3Id, SYSUTCDATETIME(), N'Booked');
    INSERT INTO attendance (session_id, member_id, checked_in_by, attendance_status, check_in_time, note, created_at)
    VALUES (@PastSession2Id, @Member1Id, @Coach1UserId, N'PRESENT', SYSUTCDATETIME(), N'Tập luyện hăng say', SYSUTCDATETIME());
    INSERT INTO attendance (session_id, member_id, checked_in_by, attendance_status, check_in_time, note, created_at)
    VALUES (@PastSession2Id, @Member3Id, @Coach1UserId, N'PRESENT', SYSUTCDATETIME(), N'Tập luyện hăng say', SYSUTCDATETIME());

    -- -----------------------------------------------------------------------------------------
    -- 8. TRAINING PLANS (CLASS PLAN & PERSONAL PLANS)
    -- -----------------------------------------------------------------------------------------
    DELETE FROM training_plans WHERE coach_id = @Coach1Id;

    DECLARE @ExercisesJsonClass NVARCHAR(MAX) = N'[
        {"Name":"Khởi động xoay khớp & Giãn cơ động","Sets":1,"Reps":1,"Duration":"10p","Intensity":"Nhẹ","Rest":0,"Notes":"Làm nóng toàn thân, xoay kỹ khớp vai và khớp gối","WeekNumber":1,"DayOfWeek":"Monday"},
        {"Name":"Barbell Bench Press (Đẩy ngực ngang)","Sets":4,"Reps":10,"Duration":null,"Intensity":"Trung bình","Rest":75,"Notes":"Hạ tạ chạm nhẹ ngực, không vểnh lưng quá mức","WeekNumber":1,"DayOfWeek":"Monday"},
        {"Name":"Incline Dumbbell Press (Đẩy ngực trên)","Sets":3,"Reps":12,"Duration":null,"Intensity":"Trung bình","Rest":60,"Notes":"Góc ghế nghiêng 30 độ, siết chặt ngực ở đỉnh","WeekNumber":1,"DayOfWeek":"Monday"},
        {"Name":"Barbell Back Squat (Gánh tạ đòn)","Sets":4,"Reps":8,"Duration":null,"Intensity":"Nặng","Rest":90,"Notes":"Xuống song song mặt đất, đầu gối mở theo hướng mũi chân","WeekNumber":1,"DayOfWeek":"Wednesday"},
        {"Name":"Romanian Deadlift (Kéo tạ đùi sau)","Sets":3,"Reps":10,"Duration":null,"Intensity":"Trung bình","Rest":75,"Notes":"Đẩy mông ra sau, giữ lưng thẳng tự nhiên","WeekNumber":1,"DayOfWeek":"Wednesday"},
        {"Name":"Lat Pulldown (Kéo xô máy)","Sets":4,"Reps":12,"Duration":null,"Intensity":"Trung bình","Rest":60,"Notes":"Kéo thanh đòn về trước ngực trên, siết chặt cơ xô","WeekNumber":1,"DayOfWeek":"Friday"},
        {"Name":"Plank Cố Định","Sets":3,"Reps":1,"Duration":"60s","Intensity":"Vừa","Rest":45,"Notes":"Siết cơ bụng và mông, giữ thân người thành đường thẳng","WeekNumber":1,"DayOfWeek":"Friday"}
    ]';

    DECLARE @ExercisesJsonPersonalAn NVARCHAR(MAX) = N'[
        {"Name":"Khởi động đi bộ dốc nhẹ trên máy","Sets":1,"Reps":1,"Duration":"10p","Intensity":"Nhẹ","Rest":0,"Notes":"Đi bộ tốc độ 4.5km/h, độ dốc 3%, giữ nhịp tim dưới 110 bpm","WeekNumber":1,"DayOfWeek":"Tuesday"},
        {"Name":"Dumbbell Goblet Squat (Squat ôm tạ trước ngực)","Sets":3,"Reps":12,"Duration":null,"Intensity":"Nhẹ","Rest":90,"Notes":"Dùng tạ ấm 12kg, kiểm soát nhịp thở đều không nín thở","WeekNumber":1,"DayOfWeek":"Tuesday"},
        {"Name":"Seated Cable Row (Kéo cáp ngồi tựa ngực)","Sets":3,"Reps":15,"Duration":null,"Intensity":"Nhẹ","Rest":75,"Notes":"Mức tạ vừa phải 30kg, tập trung cảm nhận cơ lưng","WeekNumber":1,"DayOfWeek":"Tuesday"},
        {"Name":"Dumbbell Shoulder Press Ngồi Tựa Lưng","Sets":3,"Reps":12,"Duration":null,"Intensity":"Nhẹ","Rest":90,"Notes":"Tựa lưng 85 độ, tránh bài nâng tạ đứng nín thở","WeekNumber":1,"DayOfWeek":"Thursday"},
        {"Name":"Đạp xe nhịp nhàng Recumbent Bike","Sets":1,"Reps":1,"Duration":"20p","Intensity":"Vừa","Rest":0,"Notes":"Duy trì nhịp tim an toàn 120-135 bpm, đốt mỡ tối ưu","WeekNumber":1,"DayOfWeek":"Thursday"}
    ]';

    DECLARE @ExercisesJsonPersonalMai NVARCHAR(MAX) = N'[
        {"Name":"Bird Dog (Mở rộng tay chân đối bên)","Sets":3,"Reps":10,"Duration":null,"Intensity":"Nhẹ","Rest":45,"Notes":"Giữ hông thăng bằng, kích hoạt cơ dựng sống sâu không võng lưng","WeekNumber":1,"DayOfWeek":"Monday"},
        {"Name":"Glute Bridge (Nâng mông cầu sàn)","Sets":4,"Reps":15,"Duration":null,"Intensity":"Nhẹ","Rest":60,"Notes":"Giữ đỉnh 2 giây, siết chặt cơ mông và sàn chậu","WeekNumber":1,"DayOfWeek":"Monday"},
        {"Name":"Deadbug Cố Định Cột Sống","Sets":3,"Reps":12,"Duration":null,"Intensity":"Nhẹ","Rest":45,"Notes":"Ép sát thắt lưng xuống mặt thảm trong suốt bài tập","WeekNumber":1,"DayOfWeek":"Wednesday"},
        {"Name":"Kéo giãn Cat-Cow Stretch","Sets":3,"Reps":10,"Duration":null,"Intensity":"Nhẹ","Rest":30,"Notes":"Di chuyển chậm rãi theo nhịp hít thở sâu","WeekNumber":1,"DayOfWeek":"Friday"}
    ]';

    -- Plan 1: Class Plan (Class 3)
    INSERT INTO training_plans (class_id, member_id, coach_id, name, description, goal, start_date, end_date, plan_type, status, exercises_json, fitness_condition, created_at, updated_at)
    VALUES (3, NULL, @Coach1Id, N'Giáo án Tăng cơ Giảm mỡ 8 tuần - Chu kỳ Kháng lực Hypertrophy',
            N'Giáo án khoa học dành cho toàn bộ học viên lớp Gym Kháng Lực, tối ưu hóa tăng cơ nạc và đốt mỡ.',
            N'Tăng khối lượng cơ nạc, giảm 3-5% mỡ cơ thể, nâng cao sức bền tim mạch',
            '2026-10-15', '2026-12-15', N'Class', N'Active', @ExercisesJsonClass, N'All Levels', SYSUTCDATETIME(), SYSUTCDATETIME());

    -- Plan 2: Personal Plan for An (Huyết áp an toàn)
    INSERT INTO training_plans (class_id, member_id, coach_id, name, description, goal, start_date, end_date, plan_type, status, exercises_json, fitness_condition, created_at, updated_at)
    VALUES (3, @Member1Id, @Coach1Id, N'Giáo án Cá nhân Kiểm soát Huyết áp & Đốt mỡ An Toàn',
            N'Thiết kế riêng cho học viên Nguyễn Văn An: Tránh các bài tập nín thở tăng áp lực lồng ngực, tập trung cardio vùng 2 đốt mỡ.',
            N'Giảm cân từ 84kg về 78kg, kiểm soát nhịp tim an toàn 120-135 bpm',
            '2026-10-18', '2026-11-30', N'Personal', N'Active', @ExercisesJsonPersonalAn, N'Beginner - Medical Managed', SYSUTCDATETIME(), SYSUTCDATETIME());

    -- Plan 3: Personal Plan for Mai (Thoát vị đĩa đệm)
    INSERT INTO training_plans (class_id, member_id, coach_id, name, description, goal, start_date, end_date, plan_type, status, exercises_json, fitness_condition, created_at, updated_at)
    VALUES (3, @Member2Id, @Coach1Id, N'Giáo án Cá nhân Phục hồi Thắt lưng L4-L5 & Cố định Cơ Lõi',
            N'Thiết kế riêng cho học viên Trần Thị Mai: Loại bỏ các bài gánh tạ nén trục cột sống, tập trung phục hồi cơ dựng sống.',
            N'Cố định cột sống, giảm 80% triệu chứng đau mỏi thắt lưng, săn chắc cơ',
            '2026-10-18', '2026-11-30', N'Personal', N'Active', @ExercisesJsonPersonalMai, N'Rehabilitation', SYSUTCDATETIME(), SYSUTCDATETIME());

    -- -----------------------------------------------------------------------------------------
    -- 9. HISTORICAL WORKOUT RESULTS & BODY METRICS (WEIGHT, BODY FAT, MUSCLE, PROGRESS REVIEWS)
    -- -----------------------------------------------------------------------------------------
    DELETE FROM training_results WHERE coach_id = @Coach1Id;
    DELETE FROM member_progress_reviews WHERE coach_id = @Coach1Id;

    -- Member An Progress History (Showing Weight Loss from 84.5kg -> 81.2kg)
    INSERT INTO training_results (member_id, coach_id, session_id, class_id, result_date, weight, body_fat_percentage, muscle_mass, workout_result, coach_feedback, created_at)
    VALUES
    (@Member1Id, @Coach1Id, @PastSession1Id, 3, '2026-10-22', 82.0, 26.9, 31.5, N'Hoàn thành 4 hiệp Squat 12kg và 3 hiệp Kéo xô. Huyết áp đo trước tập 125/82 mmHg.', N'Thực hiện bài tập rất tốt, kiểm soát nhịp thở đều đặn.', SYSUTCDATETIME()),
    (@Member1Id, @Coach1Id, @PastSession2Id, 3, '2026-10-24', 81.2, 26.2, 31.8, N'Đẩy ngực tạ đơn 10kg x 12 reps, đi bộ dốc 20 phút đốt 180 kcal.', N'Cân nặng giảm thêm 0.8kg, cơ thể phản hồi rất tích cực.', SYSUTCDATETIME()),
    (@Member1Id, @Coach1Id, NULL, 3, '2026-10-15', 83.5, 27.5, 31.2, N'Buổi test thể lực đầu chu kỳ 8 tuần. Nhịp tim trung bình 128 bpm.', N'Cần duy trì bổ sung nước trong buổi tập.', SYSUTCDATETIME()),
    (@Member1Id, @Coach1Id, NULL, 3, '2026-10-08', 84.5, 28.2, 31.0, N'Buổi cân đo chỉ số cơ thể InBody ban đầu.', N'Mục tiêu giảm 5kg mỡ trong 2 tháng.', SYSUTCDATETIME());

    -- Progress Reviews for An
    INSERT INTO member_progress_reviews (member_id, coach_id, review_date, progress_score, weight, body_fat_percentage, review_note, next_goal, created_at)
    VALUES
    (@Member1Id, @Coach1Id, '2026-10-08', 7.5, 84.5, 28.2, N'Đo chỉ số InBody ban đầu. Thể lực cơ bản ổn định, huyết áp cần theo dõi.', N'Thích nghi lịch tập 3 buổi/tuần', SYSUTCDATETIME()),
    (@Member1Id, @Coach1Id, '2026-10-24', 9.0, 81.2, 26.2, N'Giảm thành công 3.3kg sau hơn 2 tuần tập luyện nghiêm túc. Tỷ lệ mỡ giảm 2.0%.', N'Đạt mốc 79kg trước ngày 15/11', SYSUTCDATETIME());

    -- Member Nam Progress History (Muscle Gain)
    INSERT INTO training_results (member_id, coach_id, session_id, class_id, result_date, weight, body_fat_percentage, muscle_mass, workout_result, coach_feedback, created_at)
    VALUES
    (@Member3Id, @Coach1Id, @PastSession1Id, 3, '2026-10-22', 75.8, 15.5, 36.0, N'Squat 90kg x 8 reps, Bench Press 75kg x 10 reps.', N'Kỹ thuật phát lực bùng nổ rất tốt.', SYSUTCDATETIME()),
    (@Member3Id, @Coach1Id, @PastSession2Id, 3, '2026-10-24', 76.2, 15.1, 36.4, N'Deadlift 110kg x 6 reps, Pull-up đeo tạ 10kg x 8 reps.', N'Form chuẩn, xẻ nét cơ xô và ngực rất rõ ràng.', SYSUTCDATETIME());

    INSERT INTO member_progress_reviews (member_id, coach_id, review_date, progress_score, weight, body_fat_percentage, review_note, next_goal, created_at)
    VALUES (@Member3Id, @Coach1Id, '2026-10-24', 9.5, 76.2, 15.1, N'Tăng 1.2kg cơ nạc trong tháng 10. Sức mạnh nâng tối đa tăng 12%.', N'Nâng mốc Deadlift lên 120kg', SYSUTCDATETIME());

    -- -----------------------------------------------------------------------------------------
    -- 10. HOMEWORK ASSIGNMENTS & SUBMISSIONS (SUBMITTED & REVIEWED)
    -- -----------------------------------------------------------------------------------------
    DELETE FROM assignment_submissions WHERE assignment_id IN (SELECT id FROM assignments WHERE coach_id = @Coach1Id);
    DELETE FROM assignments WHERE coach_id = @Coach1Id;

    -- Assignment 1: Giao cho cả lớp Class 3
    INSERT INTO assignments (coach_id, class_id, member_id, title, description, due_date, status, created_at)
    VALUES (@Coach1Id, 3, NULL, N'Thử thách cuối tuần: Plank 90 giây & Hít đất chuẩn form 15 reps',
            N'Học viên tự quay video góc nghiêng thấy rõ đường thẳng lưng, giữ plank ít nhất 60-90s, sau đó thực hiện 15 lần hít đất ngực chạm gối/sàn để HLV chấm điểm form.',
            '2026-10-28 23:59:59', N'Active', SYSUTCDATETIME());
    DECLARE @Assignment1Id BIGINT = SCOPE_IDENTITY();

    -- Assignment 2: Giao riêng cho học viên Mai (Phục hồi thắt lưng)
    INSERT INTO assignments (coach_id, class_id, member_id, title, description, due_date, status, created_at)
    VALUES (@Coach1Id, 3, @Member2Id, N'Bài tập thở bụng Diaphragmatic Breathing & Kéo giãn trước khi ngủ',
            N'Thực hiện 10 phút hít thở sâu bằng cơ hoành, giãn cơ đùi sau 3 hiệp trước khi ngủ để giải tỏa áp lực thắt lưng.',
            '2026-10-29 22:00:00', N'Active', SYSUTCDATETIME());
    DECLARE @Assignment2Id BIGINT = SCOPE_IDENTITY();

    -- Submission 1 from An: Status 'Submitted' -> CHỜ COACH CHẤM ĐIỂM & ĐÁNH GIÁ!
    INSERT INTO assignment_submissions (assignment_id, member_id, submitted_at, content, score, coach_feedback, status)
    VALUES (@Assignment1Id, @Member1Id, SYSUTCDATETIME(),
            N'Thưa thầy, em đã hoàn thành bài tập. Em giữ được 75 giây plank và 12 reps hít đất. Em có đính kèm video clip quay góc nghiêng ạ.',
            NULL, NULL, N'Submitted');

    -- Submission 2 from Nam: Status 'Reviewed' -> ĐÃ ĐƯỢC CHẤM ĐIỂM
    INSERT INTO assignment_submissions (assignment_id, member_id, submitted_at, content, score, coach_feedback, status)
    VALUES (@Assignment1Id, @Member3Id, DATEADD(hour, -5, SYSUTCDATETIME()),
            N'Đã hoàn thành 90s plank và 20 reps hít đất bùng nổ, không bị cong võng lưng.',
            9.5, N'Rất xuất sắc Nam ơi! Giữ vững nhịp thở và siết chặt cơ mông như vậy nhé.', N'Reviewed');

    -- -----------------------------------------------------------------------------------------
    -- 11. COACH WORK EVENTS (PERSONAL TRAINING 1:1, SHIFT, WORKSHOP)
    -- -----------------------------------------------------------------------------------------
    DELETE FROM coach_work_events WHERE coach_id = @Coach1Id;

    INSERT INTO coach_work_events (center_id, coach_id, member_id, room_id, title, event_type, start_at, end_at, status, notes, created_by, created_at)
    VALUES
    -- PT Session Today (Completed)
    (@CenterId, @Coach1Id, @Member1Id, 4, N'PT 1:1 Kháng lực an toàn - Học viên Nguyễn Văn An', N'PersonalTraining', '2026-10-25 14:00:00', '2026-10-25 15:00:00', N'Completed', N'Tập trung kiểm soát nhịp tim và chỉnh form Dumbbell Press', @Coach1UserId, SYSUTCDATETIME()),
    -- PT Session Tomorrow (Scheduled)
    (@CenterId, @Coach1Id, @Member2Id, 5, N'PT 1:1 Phục hồi chức năng cột sống - Học viên Trần Thị Mai', N'PersonalTraining', '2026-10-26 10:00:00', '2026-10-26 11:00:00', N'Scheduled', N'Thực hành chuỗi bài tập thảm và máy kéo giãn Reformer', @Coach1UserId, SYSUTCDATETIME()),
    -- Shift
    (@CenterId, @Coach1Id, NULL, 4, N'Ca trực hướng dẫn kỹ thuật phòng tập High-Performance', N'Shift', '2026-10-28 13:00:00', '2026-10-28 17:00:00', N'Scheduled', N'Hỗ trợ hội viên mới sử dụng máy tập an toàn', @Coach1UserId, SYSUTCDATETIME()),
    -- Workshop
    (@CenterId, @Coach1Id, NULL, 1, N'Workshop Dinh Dưỡng Thể Thao & Đốt Mỡ Khoa Học', N'Workshop', '2026-10-31 14:00:00', '2026-10-31 16:30:00', N'Scheduled', N'Chia sẻ phương pháp tính TDEE, Macro và thực đơn giảm mỡ', @Coach1UserId, SYSUTCDATETIME());

    -- -----------------------------------------------------------------------------------------
    -- 12. COACH LEAVE REQUESTS & REPLACEMENT COACH (DUYỆT ĐƠN & ĐỔI CA)
    -- -----------------------------------------------------------------------------------------
    DELETE FROM coach_leaves WHERE coach_id = @Coach1Id;

    -- Leave 1: PENDING (For Manager approval test & Coach replacement candidate options)
    INSERT INTO coach_leaves (coach_id, start_at, end_at, status, leave_type, reason, swap_coach_id, requested_at)
    VALUES (@Coach1Id, '2026-10-29 13:00:00', '2026-10-29 18:00:00', N'Pending', N'Personal',
            N'Bận việc gia đình buổi chiều, đề xuất HLV Đặng Minh Tuấn phụ trách ca chiều 18h lớp Gym', @Coach2Id, SYSUTCDATETIME());

    -- Leave 2: APPROVED (Past approved leave)
    INSERT INTO coach_leaves (coach_id, start_at, end_at, status, leave_type, reason, swap_coach_id, requested_at, reviewed_at, reviewed_by, manager_note)
    VALUES (@Coach1Id, '2026-10-15 08:00:00', '2026-10-15 12:00:00', N'Approved', N'Sick',
            N'Khám sức khỏe định kỳ', @Coach2Id, '2026-10-14 09:00:00', '2026-10-14 15:00:00', @ManagerUserId, N'Đã phê duyệt, HLV Tuấn đã nhận ca');

    -- -----------------------------------------------------------------------------------------
    -- 13. COACH MESSAGES & DIRECT CHAT HISTORY
    -- -----------------------------------------------------------------------------------------
    DELETE FROM coach_messages WHERE coach_id = @Coach1Id;

    INSERT INTO coach_messages (coach_id, member_id, sender_user_id, content, created_at, read_at)
    VALUES
    (@Coach1Id, @Member1Id, 4, N'Thầy ơi, hôm nay em đo huyết áp lúc sáng thấy hơi cao 135/85, tối nay tập có cần giảm mức tạ không ạ?', DATEADD(minute, -120, SYSUTCDATETIME()), SYSUTCDATETIME()),
    (@Coach1Id, @Member1Id, @Coach1UserId, N'Chào An, em cứ khởi động kỹ 10-15 phút nhé. Tối nay mình tập tạ nhẹ 60% 1RM, tăng số reps lên 15 và tuyệt đối không nín thở khi đẩy tạ!', DATEADD(minute, -100, SYSUTCDATETIME()), SYSUTCDATETIME()),
    (@Coach1Id, @Member1Id, 4, N'Dạ em hiểu rồi thầy, tối nay em đến đúng 20h ạ!', DATEADD(minute, -80, SYSUTCDATETIME()), SYSUTCDATETIME()),

    (@Coach1Id, @Member2Id, 5, N'HLV ơi, bài Bird Dog hôm nay em tập cảm thấy thắt lưng nhẹ hẳn đi, không còn bị nhói nữa.', DATEADD(minute, -60, SYSUTCDATETIME()), SYSUTCDATETIME()),
    (@Coach1Id, @Member2Id, @Coach1UserId, N'Rất tốt Mai ơi! Bài tập này kích hoạt cơ dựng sống sâu rất hiệu quả. Cố gắng duy trì mỗi ngày 3 hiệp nhé.', DATEADD(minute, -40, SYSUTCDATETIME()), SYSUTCDATETIME());

    -- -----------------------------------------------------------------------------------------
    -- 14. NOTIFICATIONS & CLASS BROADCAST
    -- -----------------------------------------------------------------------------------------
    INSERT INTO notifications (sender_id, title, message, notification_type, target_class_ids, created_at)
    VALUES
    (@Coach1UserId, N'Thông báo lịch tập và chuẩn bị trang phục tuần mới',
     N'Chào cả lớp Gym Kháng Lực, tuần này chúng ta bước vào chu kỳ tăng tải (Progressive Overload). Các bạn nhớ mang giày đế bằng và bình nước cá nhân nhé!',
     N'Broadcast', N'[3]', SYSUTCDATETIME()),
    (@Coach1UserId, N'Giáo án tập luyện 8 tuần đã được cập nhật',
     N'HLV Phạm Quang Huy đã cập nhật đầy đủ bài tập và số hiệp trong mục Giáo án & Tiến độ. Các bạn kiểm tra trước ca tập tối nay!',
     N'TrainingPlanUpdated', N'[3]', SYSUTCDATETIME());

    -- -----------------------------------------------------------------------------------------
    -- 15. COACH COMPENSATION POLICY & KPI / SALARY DATA
    -- -----------------------------------------------------------------------------------------
    DELETE FROM coach_compensation_policies WHERE coach_id = @Coach1Id;
    DELETE FROM coach_sales_credits WHERE coach_id = @Coach1Id;

    INSERT INTO coach_compensation_policies (coach_id, monthly_base_salary, commission_per_session, sales_commission_percent, target_sessions, target_sales, effective_from, effective_to, status, updated_by, updated_at)
    VALUES (@Coach1Id, 12000000.0, 200000.0, 5.0, 30, 50000000.0, '2026-01-01', NULL, N'Active', @AdminUserId, SYSUTCDATETIME());

    INSERT INTO coach_sales_credits (coach_id, member_id, subscription_id, amount, credited_at, note, created_by)
    VALUES (@Coach1Id, @Member1Id, NULL, 5000000.0, '2026-10-10', N'Hoa hồng tư vấn gói tập VIP Platinum Hội viên Nguyễn Văn An', @ManagerUserId);

    COMMIT TRANSACTION;
    PRINT N'SUCCESS: Coach full functional test data seeded successfully!';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrorState INT = ERROR_STATE();
    RAISERROR (@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;

GO
