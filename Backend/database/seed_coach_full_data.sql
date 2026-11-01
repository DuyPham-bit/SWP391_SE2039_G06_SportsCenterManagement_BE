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
