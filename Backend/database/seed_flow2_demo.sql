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
    DECLARE @PasswordHash NVARCHAR(255) = N'__FLOW2_PASSWORD_HASH__';

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
