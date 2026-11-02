using SportsCenterManagement.BLL.DTOs.Training;

namespace SportsCenterManagement.BLL.Services;

public sealed record AiWorkoutSafetyProfile(
    decimal Bmi,
    string EffectiveGoal,
    string EffectiveLevel,
    string[] AcceptedTags,
    string[] DiscardedTags,
    string[] MedicalConditions,
    string[] ConflictWarnings,
    string[] MandatoryAdjustments,
    bool IsUnderweight,
    bool IsSeverelyUnderweight,
    bool IsOverweight,
    bool HasSevereInjury,
    bool HasBackIssue,
    bool HasWristOrShoulderIssue,
    bool HasKneeIssue,
    bool HasHypertension,
    bool WantsMuscleGain,
    bool WantsFatLoss,
    bool WantsEndurance,
    bool HasSevereLegInjury = false,
    string[]? ExtractedTags = null,
    string? AiReasoning = null);

public static class AiWorkoutSafetyPolicy
{
    private static readonly HashSet<string> KnownTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "skinny_gain", "fit_meso", "overweight_endo", "low_stamina", "high_endurance",
        "back_pain", "knee_pain", "shoulder_pain", "wrist_pain", "hypertension",
        "kyphosis_posture", "severe_injury", "mass_focus", "machine_focus", "core_focus", "low_impact"
    };

    private static readonly string[] SevereInjuryTerms =
    [
        "gãy", "nứt xương", "đứt dây chằng", "đứt chằng", "rách dây chằng", "vừa mổ", "sau mổ",
        "phẫu thuật", "bó bột", "trật khớp", "fracture", "ligament tear", "post-surgery", "surgery"
    ];

    private static readonly string[] BackTerms =
    [
        "đau lưng", "thoát vị", "gù lưng", "cột sống", "back pain", "herniat", "kyphosis"
    ];

    private static readonly string[] WristShoulderTerms =
    [
        "cổ tay", "đau khớp vai", "đau vai", "chóp xoay", "wrist", "shoulder", "rotator cuff"
    ];

    private static readonly string[] KneeTerms =
    [
        "đau gối", "khớp gối", "sụn gối", "knee", "patella", "meniscus"
    ];

    private static readonly string[] HypertensionTerms =
    [
        "cao huyết áp", "huyết áp cao", "hypertension", "bệnh tim mạch", "cardiovascular disease"
    ];

    private static readonly string[] FatLossTerms =
    [
        "giảm cân", "giảm mỡ", "fat loss", "weight loss", "hiit"
    ];

    private static readonly string[] MuscleGainTerms =
    [
        "tăng cơ", "khối lượng cơ", "sức mạnh", "muscle gain", "hypertrophy", "strength"
    ];

    private static readonly string[] EnduranceTerms =
    [
        "sức bền", "thể lực", "tim mạch", "endurance", "stamina", "cardio"
    ];

    private static readonly string[] BackBannedExercises =
    [
        "barbell back squat", "front squat", "zercher squat", "hack squat", "good morning",
        "conventional deadlift", "sumo deadlift", "trap bar deadlift", "barbell deadlift",
        "romanian deadlift", "stiff-leg deadlift", "overhead press", "military press",
        "barbell lunge", "farmer carry", "farmer's carry", "bent-over row", "pendlay row"
    ];

    private static readonly string[] WristShoulderBannedExercises =
    [
        "barbell bench press", "barbell press", "straight barbell press", "barbell overhead press", "military press"
    ];

    private static readonly string[] HighImpactExercises =
    [
        "jump", "depth drop", "burpee", "plyometric", "bounding", "high knees", "jumping lunge"
    ];

    private static readonly string[] KneeBannedExercises =
    [
        "deep squat", "atg squat", "ass-to-grass squat", "sissy squat"
    ];

    private static readonly string[] LowerLimbImmobilizationTerms =
    [
        "gãy chân", "bó bột", "trật chân", "trật cổ chân", "đứt dây chằng", "mới mổ", "phẫu thuật chân",
        "chấn thương chân", "gãy xương", "nạng", "ngồi hoàn toàn", "bất động chân", "đau cổ chân",
        "bong gân cổ chân", "leg cast", "broken leg", "ankle sprain", "ankle fracture", "acl tear",
        "meniscus tear", "immobiliz"
    ];

    private static readonly string[] LowerLimbBannedExercises =
    [
        "squat", "deadlift", "lunge", "jump", "burpee", "run", "sprint", "step-up",
        "box jump", "calf raise", "leg press"
    ];

    private static readonly string[] SevereInjuryBannedExercises =
    [
        "squat", "deadlift", "lunge", "sprint", "running", "run ", "treadmill", "step-up", "step up",
        "calf raise", "box jump", "jump"
    ];

    public static AiWorkoutSafetyProfile Resolve(
        RecommendationContext context,
        string requestedGoal,
        string currentLevel,
        IEnumerable<string>? subjectiveTags,
        string? coachNotes = null)
    {
        if (context.HeightCm is null or <= 0 || context.WeightKg is null or <= 0 ||
            string.IsNullOrWhiteSpace(context.FitnessGoal) || context.Bmi is null or <= 0)
            throw new InvalidOperationException("Học viên cần cập nhật đủ chiều cao, cân nặng và mục tiêu tập luyện trước khi dùng AI.")
            {
                Data =
                {
                    ["StatusCode"] = 400,
                    ["Code"] = "INSUFFICIENT_CONTEXT_FOR_AI",
                    ["ProposedHandling"] = "Cập nhật chiều cao, cân nặng và mục tiêu trong hồ sơ hội viên rồi thử lại."
                }
            };

        var tags = (subjectiveTags ?? []).Where(tag => !string.IsNullOrWhiteSpace(tag) && tag.Length <= 50)
            .Select(tag => tag.Trim()).Where(KnownTags.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var discarded = new List<string>();
        var warnings = new List<string>();
        var adjustments = new List<string>();
        var conditions = new List<string>();
        var bmi = context.Bmi.Value;
        var isUnderweight = bmi < 18.5m;
        var isSeverelyUnderweight = bmi < 16m;
        var isOverweight = bmi > 25m;
        var medicalText = string.Join("\n", new[] { context.MedicalNote }
            .Concat((context.HealthRecords ?? []).SelectMany(record => new[] { record.Title, record.Details }))
            .Where(value => !string.IsNullOrWhiteSpace(value))).ToLowerInvariant();
        var subjectiveHealthText = coachNotes?.Trim() ?? string.Empty;
        var hasSevereInjury = ContainsAny(medicalText, SevereInjuryTerms) ||
            ContainsAny(subjectiveHealthText, SevereInjuryTerms) ||
            tags.Contains("severe_injury", StringComparer.OrdinalIgnoreCase);
        var hasBackIssue = ContainsAny(medicalText, BackTerms) || ContainsAny(subjectiveHealthText, BackTerms) ||
            tags.Any(tag => tag.Equals("back_pain", StringComparison.OrdinalIgnoreCase) ||
                tag.Equals("kyphosis_posture", StringComparison.OrdinalIgnoreCase));
        var hasWristOrShoulderIssue = ContainsAny(medicalText, WristShoulderTerms) || ContainsAny(subjectiveHealthText, WristShoulderTerms) ||
            tags.Any(tag => tag.Equals("wrist_pain", StringComparison.OrdinalIgnoreCase) ||
                tag.Equals("shoulder_pain", StringComparison.OrdinalIgnoreCase));
        var hasKneeIssue = ContainsAny(medicalText, KneeTerms) || ContainsAny(subjectiveHealthText, KneeTerms) ||
            tags.Contains("knee_pain", StringComparer.OrdinalIgnoreCase);
        var hasHypertension = ContainsAny(medicalText, HypertensionTerms) || ContainsAny(subjectiveHealthText, HypertensionTerms) ||
            tags.Contains("hypertension", StringComparer.OrdinalIgnoreCase);
        var hasSevereLegInjury = ContainsAny(medicalText, LowerLimbImmobilizationTerms) ||
            ContainsAny(subjectiveHealthText, LowerLimbImmobilizationTerms);

        if (!string.IsNullOrWhiteSpace(context.MedicalNote)) conditions.Add(context.MedicalNote.Trim());
        conditions.AddRange((context.HealthRecords ?? []).Select(record => $"{record.Title}: {record.Details}"));

        var effectiveGoal = requestedGoal.Trim();
        var effectiveLevel = currentLevel.Trim();

        if (hasSevereLegInjury)
        {
            warnings.Add($"Phát hiện chấn thương chi dưới/bó bột trong mô tả của HLV; hệ thống đã ghi đè mục tiêu sang phục hồi chức năng và tập cô lập vùng lành lặn ở tư thế ngồi.");
            effectiveGoal = "Phục hồi chức năng và tập cô lập thân trên tại chỗ (Tư thế ngồi có tựa / Không chịu tải chi dưới)";
            effectiveLevel = "Rehabilitation - Seated Isolation";
            adjustments.Add("CẤM 100% các bài tập đứng chịu tải chân, chạy nhảy hoặc di chuyển; toàn bộ bài tập bắt buộc thực hiện ở tư thế ngồi có tựa hoặc nằm (Seated / Floor / Chest-supported).");
        }

        if (isUnderweight)
        {
            var discardedBeforeBmiOverride = discarded.Count;
            Discard(tags, discarded, "fit_meso", "overweight_endo", "high_endurance");
            if (ContainsAny(effectiveGoal, FatLossTerms))
            {
                warnings.Add($"Mục tiêu chủ quan '{effectiveGoal}' không phù hợp với BMI {bmi:0.0}; hệ thống đã ưu tiên phục hồi thể trạng và tăng khối lượng nạc.");
                effectiveGoal = "Phục hồi thể trạng, tăng cân và xây dựng khối lượng cơ nạc";
            }
            if (discarded.Count > discardedBeforeBmiOverride)
                warnings.Add($"BMI {bmi:0.0} thuộc nhóm thiếu cân; đã vô hiệu hóa các thẻ thể trạng/sức bền không phù hợp.");
            adjustments.Add("Bắt buộc thặng dư 300-500 kcal/ngày và tập khối lượng thấp, cường độ vừa, 2-3 buổi/tuần.");
        }
        else if (isOverweight)
        {
            var discardedBeforeBmiOverride = discarded.Count;
            Discard(tags, discarded, "skinny_gain", "fit_meso");
            if (discarded.Count > discardedBeforeBmiOverride)
                warnings.Add($"BMI {bmi:0.0} thuộc nhóm thừa cân; đã vô hiệu hóa thẻ tạng người mâu thuẫn với dữ liệu sinh trắc học.");
        }
        else
        {
            var discardedBeforeBmiOverride = discarded.Count;
            Discard(tags, discarded, "skinny_gain", "overweight_endo");
            if (discarded.Count > discardedBeforeBmiOverride)
            {
                warnings.Add($"BMI {bmi:0.0} nằm trong khoảng bình thường; đã vô hiệu hóa thẻ 'Gầy/khó tăng cân' hoặc 'Thừa cân' vì không phù hợp với dữ liệu khách quan.");
                adjustments.Add($"Giữ nguyên trình độ {effectiveLevel}; không tự động hạ cấp bài tập chỉ vì thẻ tạng người đã bị loại.");
            }
        }

        var wantsMuscleGain = ContainsAny(effectiveGoal, MuscleGainTerms);
        var wantsFatLoss = ContainsAny(effectiveGoal, FatLossTerms);
        var wantsEndurance = ContainsAny(effectiveGoal, EnduranceTerms);
        if (wantsMuscleGain && wantsFatLoss && !hasSevereLegInjury)
        {
            effectiveGoal = wantsEndurance
                ? "Tái cấu trúc cơ thể (tăng cơ, giảm mỡ) và cải thiện sức bền"
                : "Tái cấu trúc cơ thể (tăng cơ và giảm mỡ)";
            warnings.Add("Tăng cơ và giảm mỡ là hai mục tiêu cạnh tranh nếu cố tối đa hóa cùng lúc; hệ thống đã chuyển sang chiến lược tái cấu trúc cơ thể với thâm hụt nhẹ hoặc duy trì năng lượng.");
            adjustments.Add("Ưu tiên protein cao, tăng tải có kiểm soát và theo dõi thành phần cơ thể; không áp dụng bulking thặng dư 400-500 kcal/ngày.");
        }

        if (isSeverelyUnderweight && !hasSevereLegInjury)
        {
            effectiveLevel = "Beginner - Medical Safety Foundation";
            adjustments.Add("Ưu tiên máy tập, dây cáp và trọng lượng cơ thể; không dùng tạ tự do nặng hoặc HIIT.");
        }

        if (hasSevereInjury)
        {
            if (!effectiveGoal.Contains("phục hồi", StringComparison.OrdinalIgnoreCase))
                warnings.Add($"Chấn thương nặng ghi đè mục tiêu '{effectiveGoal}'; hệ thống chuyển sang phục hồi chức năng và tập cô lập vùng an toàn.");
            effectiveGoal = "Phục hồi chức năng và tập cô lập vùng an toàn";
            effectiveLevel = "Beginner - Medical Rehabilitation";
            adjustments.Add("Cấm squat, lunge, deadlift, chạy, nhảy và mọi bài chịu tải lên vùng chấn thương; chỉ tập cô lập vùng an toàn ở Zone 1-2.");
        }

        if (isOverweight)
            adjustments.Add("Bắt buộc thâm hụt calo có kiểm soát; loại bỏ bật nhảy và plyometric cường độ cao để bảo vệ khớp.");

        if (hasBackIssue)
        {
            adjustments.Add("Cấm tải dọc trục cột sống: Barbell Back Squat, Conventional Deadlift và Overhead Press; dùng máy có tựa, Chest-supported Row, Face Pull và Core Stabilization.");
        }

        if (hasWristOrShoulderIssue)
        {
            adjustments.Add("Cấm các bài đẩy tạ đòn thẳng; dùng tạ đơn Neutral Grip, máy cáp hoặc Floor Press giới hạn duỗi vai.");
        }

        if (hasKneeIssue)
        {
            adjustments.Add("Cấm squat quá sâu và động tác bật nhảy/va đập cao; ưu tiên Seated Leg Curl, Hip Thrust hoặc Glute Bridge với biên độ không đau.");
        }

        if (hasHypertension)
        {
            adjustments.Add("Không nín thở kiểu Valsalva hoặc tập gắng sức tối đa; duy trì nhịp thở liên tục và nhịp tim dưới 75% HRmax.");
        }

        if (effectiveLevel.Equals("Beginner", StringComparison.OrdinalIgnoreCase))
            adjustments.Add("Ưu tiên máy tập, dây cáp và trọng lượng cơ thể để xây dựng kiểm soát thần kinh-cơ trước khi tăng tải.");

        var extractedTagsList = new List<string>();
        if (hasSevereLegInjury) extractedTagsList.Add("Gãy chân / chi dưới");
        else if (hasBackIssue) extractedTagsList.Add("Cột sống / thoát vị");
        else if (hasKneeIssue) extractedTagsList.Add("Khớp gối yếu");
        else if (hasWristOrShoulderIssue) extractedTagsList.Add("Vai / cổ tay");
        else if (hasHypertension) extractedTagsList.Add("Huyết áp cao");

        if (subjectiveHealthText.Contains("thể lực yếu", StringComparison.OrdinalIgnoreCase) ||
            subjectiveHealthText.Contains("hụt hơi", StringComparison.OrdinalIgnoreCase))
            extractedTagsList.Add("thể lực yếu");
        else if (isSeverelyUnderweight) extractedTagsList.Add("suy nhược (<16 BMI)");
        else if (isUnderweight) extractedTagsList.Add("thiếu cân (BMI < 18.5)");
        else if (isOverweight) extractedTagsList.Add("thừa cân (BMI > 25)");

        if (hasSevereLegInjury) extractedTagsList.Add("tập ngồi tĩnh (Seated)");
        else if (wantsMuscleGain && wantsFatLoss) extractedTagsList.Add("tái cấu trúc cơ thể");
        else if (!string.IsNullOrWhiteSpace(effectiveGoal)) extractedTagsList.Add(effectiveGoal.ToLowerInvariant());

        var aiReasoning = hasSevereLegInjury
            ? "Phát hiện chấn thương gãy chân, hệ thống đã chuyển sang chế độ tập ngồi có tựa lưng (cấm 100% bài đứng/chịu lực chân) và hạ nhịp tim về Zone 1–2."
            : warnings.Count > 0
                ? warnings[0]
                : (adjustments.Count > 0 ? adjustments[0] : $"Thiết kế phác đồ an toàn theo BMI {bmi:0.0} và thể trạng học viên.");

        return new AiWorkoutSafetyProfile(bmi, effectiveGoal, effectiveLevel, tags.ToArray(), discarded.ToArray(),
            conditions.ToArray(), warnings.Distinct().ToArray(), adjustments.Distinct().ToArray(), isUnderweight,
            isSeverelyUnderweight, isOverweight, hasSevereInjury, hasBackIssue, hasWristOrShoulderIssue, hasKneeIssue, hasHypertension,
            wantsMuscleGain, wantsFatLoss, wantsEndurance, hasSevereLegInjury, extractedTagsList.ToArray(), aiReasoning);
    }

    public static string[] FindExerciseViolations(AiWorkoutSafetyProfile profile, IEnumerable<string> exerciseNames)
    {
        var violations = new List<string>();
        var names = exerciseNames.Select(name => name?.Trim() ?? string.Empty).ToArray();
        foreach (var name in names)
        {
            var normalized = name.ToLowerInvariant();
            if (profile.HasSevereInjury && ContainsAny(normalized, SevereInjuryBannedExercises))
                violations.Add($"'{name}' tạo tải lên vùng đang chấn thương nặng.");
            if ((profile.IsSeverelyUnderweight || profile.HasBackIssue) && ContainsAny(normalized, BackBannedExercises))
                violations.Add($"'{name}' tạo tải cột sống hoặc mức tải không phù hợp với hồ sơ hiện tại.");
            if (profile.HasWristOrShoulderIssue && ContainsAny(normalized, WristShoulderBannedExercises))
                violations.Add($"'{name}' không phù hợp với vấn đề cổ tay/vai.");
            if (profile.IsOverweight && ContainsAny(normalized, HighImpactExercises))
                violations.Add($"'{name}' là bài bật nhảy/va đập cao không phù hợp với BMI hiện tại.");
            if (profile.HasKneeIssue && (ContainsAny(normalized, HighImpactExercises) || ContainsAny(normalized, KneeBannedExercises)))
                violations.Add($"'{name}' tạo va đập hoặc biên độ gối không phù hợp với tình trạng khớp gối.");
            if (profile.HasSevereLegInjury && ContainsAny(normalized, LowerLimbBannedExercises) && !normalized.Contains("seated") && !normalized.Contains("lying") && !normalized.Contains("floor"))
                violations.Add($"'{name}' tạo áp lực lên chi dưới đang bị tổn thương/chấn thương.");
        }

        if (profile.HasSevereLegInjury)
        {
            RequireOne(names, violations, ["seated", "chest-supported", "lying", "floor", "tựa"],
                "Bắt buộc các bài tập phải thực hiện ở tư thế ngồi có tựa lưng (Seated) hoặc nằm để bảo vệ chân tổn thương.");
        }

        if (profile.HasBackIssue)
        {
            RequireOne(names, violations, ["seated", "machine"], "Thiếu bài máy có tựa hoặc bài ngồi để tránh tải dọc trục.");
            RequireOne(names, violations, ["chest-supported"], "Thiếu Chest-supported Row theo điều chỉnh cột sống bắt buộc.");
            RequireOne(names, violations, ["face pull"], "Thiếu Face Pull theo điều chỉnh tư thế bắt buộc.");
            RequireOne(names, violations, ["deadbug", "dead bug", "plank", "bird-dog", "bird dog"],
                "Thiếu bài ổn định cơ lõi không nén cột sống.");
        }
        if (profile.HasWristOrShoulderIssue)
            RequireOne(names, violations, ["neutral-grip", "neutral grip", "cable", "floor press", "machine press"],
                "Thiếu bài đẩy/kéo Neutral Grip, Cable, Machine hoặc Floor Press để bảo vệ cổ tay/vai.");
        if (profile.HasKneeIssue)
            RequireOne(names, violations, ["seated leg curl", "hip thrust", "glute bridge"],
                "Thiếu bài Seated Leg Curl, Hip Thrust hoặc Glute Bridge có biên độ kiểm soát để bảo vệ khớp gối.");

        return violations.Distinct().ToArray();
    }

    private static bool ContainsAny(string value, IEnumerable<string> terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static void Discard(List<string> tags, List<string> discarded, params string[] rejected)
    {
        foreach (var tag in rejected)
            if (tags.RemoveAll(value => value.Equals(tag, StringComparison.OrdinalIgnoreCase)) > 0) discarded.Add(tag);
    }

    private static void RequireOne(string[] exerciseNames, List<string> violations, string[] acceptedTerms, string message)
    {
        if (!exerciseNames.Any(name => ContainsAny(name, acceptedTerms))) violations.Add(message);
    }
}
