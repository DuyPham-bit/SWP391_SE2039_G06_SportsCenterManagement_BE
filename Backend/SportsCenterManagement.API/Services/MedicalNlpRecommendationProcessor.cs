using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SportsCenterManagement.BLL.Services;

namespace SportsCenterManagement.API.Services;

public static class MedicalNlpRecommendationProcessor
{
    private static readonly string[] SevereInjuryTerms =
    [
        "gãy", "nứt xương", "đứt dây chằng", "đứt chằng", "rách dây chằng", "vừa mổ", "sau mổ",
        "phẫu thuật", "bó bột", "trật khớp", "fracture", "ligament tear", "post-surgery", "surgery"
    ];

    private static readonly string[] HerniaTerms = ["thoát vị", "herniat", "đĩa đệm", "l4-l5"];

    private static readonly string[] BackTerms =
    [
        "đau lưng", "đau cột sống", "cột sống", "đau thắt lưng", "thắt lưng", "đau l4", "l4-l5",
        "lower back", "back pain", "spine", "gù", "thoát vị"
    ];

    private static readonly string[] KneeTerms =
    [
        "đau gối", "khớp gối", "đứt dây chằng", "sụn chêm", "trật khớp gối", "phẫu thuật gối",
        "knee pain", "weak knee", "ligament"
    ];

    private static readonly string[] ShoulderWristTerms =
    [
        "đau vai", "khớp vai", "chóp xoay", "đau cổ tay", "cổ tay", "hội chứng ống cổ tay",
        "shoulder", "rotator cuff", "wrist pain", "weak wrist", "carpal"
    ];

    private static readonly string[] HypertensionTerms =
    [
        "huyết áp", "cao huyết áp", "tim mạch", "tim đập nhanh", "nhịp tim nhanh", "loạn nhịp",
        "hypertension", "blood pressure", "heart", "tachycardia"
    ];

    private static readonly string[] HeavyGoalTerms =
    [
        "nâng cao", "nâng tạ", "nâng nặng", "tăng tốc", "nhảy cao", "nhảy xa", "chạy nước rút",
        "sprint", "tăng sức mạnh", "sức mạnh tối đa", "hiit", "plyometric", "powerlifting", "max effort", "1rm"
    ];

    private static readonly string[] AxialBackBannedExercises =
    [
        "barbell back squat", "back squat", "deadlift", "overhead press", "military press",
        "barbell squat", "heavy squat", "snatch", "clean and jerk", "romanian deadlift"
    ];

    private static readonly string[] SevereInjuryBannedExercises =
    [
        "squat", "deadlift", "lunge", "sprint", "running", "run ", "treadmill", "step-up", "step up",
        "calf raise", "box jump", "jump"
    ];

    public static JsonObject Process(
        string reply,
        AiWorkoutSafetyProfile safety,
        decimal weightKg,
        string? coachNotes,
        string? coachGoal)
    {
        var rawJson = ExtractJson(reply);
        JsonObject parsed;
        try
        {
            parsed = JsonNode.Parse(rawJson) as JsonObject ?? new JsonObject();
        }
        catch
        {
            parsed = new JsonObject();
        }

        var fullContextText = $"{coachNotes} {coachGoal} {safety.EffectiveGoal}".ToLowerInvariant();
        var hasSevereInjury = safety.HasSevereInjury || ContainsAny(fullContextText, SevereInjuryTerms) ||
                              safety.AcceptedTags.Contains("severe_injury", StringComparer.OrdinalIgnoreCase);
        var hasHernia = ContainsAny(fullContextText, HerniaTerms) ||
                        safety.AcceptedTags.Contains("hernia", StringComparer.OrdinalIgnoreCase);
        var hasBack = hasHernia || ContainsAny(fullContextText, BackTerms) || safety.HasBackIssue;
        var hasKnee = ContainsAny(fullContextText, KneeTerms) || safety.HasKneeIssue;
        var hasShoulderWrist = ContainsAny(fullContextText, ShoulderWristTerms) || safety.HasWristOrShoulderIssue;
        var hasHypertension = ContainsAny(fullContextText, HypertensionTerms) || safety.HasHypertension;

        // 1. Tự động bóc tách từ khóa cốt lõi kèm Emoji
        var detectedKeywords = new List<string>();
        if (hasSevereInjury) detectedKeywords.Add("⚠️ Chấn thương nặng / Sau mổ");
        if (hasHernia) detectedKeywords.Add("⚠️ Thoát vị đĩa đệm L4-L5");
        else if (hasBack) detectedKeywords.Add("🦴 Đau lưng / Cột sống");
        if (hasKnee) detectedKeywords.Add("🦵 Khớp gối yếu / Chấn thương gối");
        if (hasShoulderWrist) detectedKeywords.Add("💪 Khớp vai & Cổ tay yếu");
        if (hasHypertension) detectedKeywords.Add("❤️ Tim mạch / Huyết áp");
        if (safety.IsSeverelyUnderweight) detectedKeywords.Add("🪫 Suy nhược cơ thể (BMI < 16)");
        else if (safety.IsUnderweight) detectedKeywords.Add("📉 Thể trạng thiếu cân (BMI < 18.5)");
        else if (safety.IsOverweight) detectedKeywords.Add("📈 Thể trạng thừa cân (BMI > 25)");

        if (fullContextText.Contains("máy khối") || fullContextText.Contains("máy tập") ||
            fullContextText.Contains("cáp") || fullContextText.Contains("cable") || fullContextText.Contains("dây kháng lực"))
            detectedKeywords.Add("🪑 Ưu tiên máy khối & Dây cáp");

        if (fullContextText.Contains("tại nhà") || fullContextText.Contains("ở nhà") || fullContextText.Contains("home"))
            detectedKeywords.Add("🏠 Tập luyện tại nhà");

        if (fullContextText.Contains("gù") || fullContextText.Contains("kyphosis") || fullContextText.Contains("vai đổ") || fullContextText.Contains("tư thế"))
            detectedKeywords.Add("🧘 Sai lệch tư thế / Vai gù");

        if (fullContextText.Contains("văn phòng") || fullContextText.Contains("ngồi nhiều") || fullContextText.Contains("cứng hông"))
            detectedKeywords.Add("💼 Dân văn phòng / Cứng hông");

        if (fullContextText.Contains("thể lực yếu") || fullContextText.Contains("nhanh mệt") || fullContextText.Contains("hụt hơi") || fullContextText.Contains("mới bắt đầu") || fullContextText.Contains("chưa từng tập"))
            detectedKeywords.Add("⚡ Thể lực yếu / Nhập môn");

        if ((safety.WantsMuscleGain && safety.WantsFatLoss) || (fullContextText.Contains("tăng cơ") && fullContextText.Contains("giảm mỡ")) || fullContextText.Contains("recomp"))
            detectedKeywords.Add("🔄 Tái cấu trúc cơ thể");
        else if (safety.WantsMuscleGain || fullContextText.Contains("tăng cơ"))
            detectedKeywords.Add("🎯 Mục tiêu tăng cơ nạc");
        else if (safety.WantsFatLoss || fullContextText.Contains("giảm mỡ"))
            detectedKeywords.Add("🔥 Mục tiêu giảm mỡ");

        if (fullContextText.Contains("sức bền") || fullContextText.Contains("dung tích phổi") || fullContextText.Contains("cardio"))
            detectedKeywords.Add("🏃 Sức bền tim mạch & Thể lực");

        if (hasSevereInjury || hasBack || hasKnee)
            detectedKeywords.Add("🪑 Chế độ tập có điểm tựa");

        // Gộp với keywords mà AI trả về nếu có
        var aiKeywordsNode = parsed["extracted_keywords"] as JsonArray ?? parsed["extractedTags"] as JsonArray;
        if (aiKeywordsNode is not null)
        {
            foreach (var node in aiKeywordsNode)
            {
                if (node is JsonValue val && val.TryGetValue<string>(out var tag) && !string.IsNullOrWhiteSpace(tag))
                {
                    var cleanTag = tag.Trim();
                    if (!detectedKeywords.Any(k => string.Equals(Normalize(k), Normalize(cleanTag), StringComparison.OrdinalIgnoreCase)))
                    {
                        detectedKeywords.Add(EnsureEmoji(cleanTag));
                    }
                }
            }
        }

        // 2. Lý giải chuyên môn Y học Thể thao (AI Clinical Reasoning)
        var aiReasoning = GetStringCoerced(parsed, "ai_reasoning", "aiReasoning", "coachReviewNote", "clinicalAssessment") ?? string.Empty;
        var needsOverride = (hasSevereInjury || hasHernia || hasBack) && ContainsAny(fullContextText, HeavyGoalTerms);
        if (needsOverride)
        {
            var overrideNote = "Hệ thống phát hiện chấn thương nghiêm trọng nhưng yêu cầu vận động nặng; đã BẮT BUỘC GHI ĐÈ mục tiêu sang phục hồi chức năng & tập cô lập có điểm tựa, loại bỏ hoàn toàn các bài nén ép cột sống/tải khớp.";
            if (!aiReasoning.Contains("GHI ĐÈ") && !aiReasoning.Contains("ghi đè"))
            {
                aiReasoning = string.IsNullOrWhiteSpace(aiReasoning) ? overrideNote : $"{aiReasoning} • {overrideNote}";
            }
        }
        else if (string.IsNullOrWhiteSpace(aiReasoning))
        {
            aiReasoning = "Phác đồ được tối ưu hóa dựa trên sinh trắc học và lịch sử sức khỏe, ưu tiên an toàn khớp và hiệu quả cơ bắp.";
        }

        // 3. Phác đồ (Protocol: Frequency & Nutrition)
        string frequency;
        string nutrition;
        if (parsed["protocol"] is JsonObject protocolObj)
        {
            frequency = GetStringCoerced(protocolObj, "frequency", "weekly_volume", "volume") ?? string.Empty;
            nutrition = GetStringCoerced(protocolObj, "nutrition", "nutrition_guidance", "diet") ?? string.Empty;
        }
        else
        {
            frequency = GetStringCoerced(parsed, "weeklyVolume", "frequency", "recommendedVolume") ?? string.Empty;
            nutrition = GetStringCoerced(parsed, "nutritionGuidance", "nutrition") ?? string.Empty;
        }

        if (hasSevereInjury)
        {
            frequency = "2-3 buổi/tuần • 30-40 phút/buổi • Cường độ thấp (Zone 1-2), tư thế có điểm tựa";
        }
        else if (safety.IsUnderweight)
        {
            frequency = safety.IsSeverelyUnderweight
                ? "2-3 buổi/tuần • 30-40 phút/buổi • Cường độ thấp đến vừa (Zone 1-2)"
                : "2-3 buổi/tuần • 35-45 phút/buổi • Cường độ vừa (Zone 2), không HIIT";
        }
        else if (hasHypertension)
        {
            frequency = "3-4 buổi/tuần • 40-50 phút/buổi • Nhịp tim Zone 1-2 (dưới 75% HRmax)";
        }
        else if (string.IsNullOrWhiteSpace(frequency))
        {
            frequency = "3-4 buổi/tuần • 45-60 phút/buổi • Zone 2 (60-70% HRmax)";
        }

        if (safety.IsUnderweight)
        {
            var minProt = Math.Round(weightKg * 1.6m);
            var maxProt = Math.Round(weightKg * 2.0m);
            nutrition = $"Thặng dư 300-500 kcal/ngày; protein tham khảo {minProt:0}-{maxProt:0}g/ngày (1.6-2.0 g/kg), chia nhỏ 4-5 bữa dễ tiêu hóa.";
        }
        else if (safety.IsOverweight)
        {
            nutrition = "Thâm hụt 300-500 kcal/ngày; ưu tiên đạm nạc 1.6-2.0 g/kg, rau xanh giàu chất xơ và uống đủ nước.";
        }
        else if (safety.WantsMuscleGain && safety.WantsFatLoss)
        {
            var minProt = Math.Round(weightKg * 1.6m);
            var maxProt = Math.Round(weightKg * 2.2m);
            nutrition = $"Duy trì năng lượng hoặc thâm hụt nhẹ 100-250 kcal/ngày; protein {minProt:0}-{maxProt:0}g/ngày (1.6-2.2 g/kg), theo dõi vòng eo, sức mạnh và cân nặng.";
        }
        else if (string.IsNullOrWhiteSpace(nutrition))
        {
            nutrition = "Duy trì năng lượng cân bằng, protein 1.6-2.2 g/kg/ngày, tăng cường carbohydrate phức hợp quanh buổi tập.";
        }

        // 4. Bóc tách & Chuẩn hóa danh sách bài tập (Exercises)
        var rawExercises = FindExercisesArray(parsed);
        var canonicalExercises = new JsonArray();

        if (rawExercises is not null)
        {
            foreach (var node in rawExercises)
            {
                if (node is not JsonObject item) continue;

                var name = GetStringCoerced(item, "name", "exercise", "title", "exercise_name") ?? "Bài tập bổ trợ";
                var volume = GetStringCoerced(item, "sets_reps", "setsReps", "volume", "reps_sets")
                             ?? (item["sets"] is not null && item["reps"] is not null
                                 ? $"{item["sets"]} hiệp x {item["reps"]} lần"
                                 : "3 hiệp x 10-12 lần");
                var focus = GetStringCoerced(item, "medical_focus", "medicalFocus", "note", "focus", "technique")
                            ?? "Thực hiện chậm, kiểm soát cơ bắp và hít thở điều hòa.";

                var (sets, reps) = SplitVolume(volume);

                canonicalExercises.Add(new JsonObject
                {
                    ["name"] = name,
                    ["sets_reps"] = volume,
                    ["medical_focus"] = focus,
                    ["sets"] = sets,
                    ["reps"] = reps,
                    ["volume"] = volume,
                    ["note"] = focus
                });
            }
        }

        // Nếu AI không trả bài tập hợp lệ hoặc quá ít, tự động bổ sung bài tập thể thao an toàn
        if (canonicalExercises.Count < 3)
        {
            canonicalExercises.Clear();
            if (hasSevereInjury)
            {
                AddExercise(canonicalExercises, "Chest-Supported Machine Row (Kéo máy có tựa ngực)", "3 hiệp x 12 lần", "Tựa ngực và lưng chắc chắn, tập cô lập thân trên mà không tải lên chi bị chấn thương.");
                AddExercise(canonicalExercises, "Seated Cable Face Pull (Kéo cáp ngang mặt)", "3 hiệp x 15 lần", "Kích hoạt vai sau và lưng trên, không tạo tải nén lên cột sống.");
                AddExercise(canonicalExercises, "Dead Bug Core Stabilization (Ổn định cơ lõi)", "3 hiệp x 10 lần mỗi bên", "Nằm ngửa, giữ lưng dưới ổn định và không chịu tải lên vùng chấn thương.");
                AddExercise(canonicalExercises, "Seated Neutral-Grip Machine Chest Press (Máy ép ngực tay cầm trung tính)", "3 hiệp x 12 lần", "Tựa lưng hoàn toàn, cổ tay trung tính và duy trì nhịp tim Zone 1-2.");
                if (hasKnee)
                    AddExercise(canonicalExercises, "Seated Leg Curl Machine (Máy cuốn đùi sau)", "2 hiệp x 12 lần", "Chỉ dùng biên độ không đau và dừng ngay khi vùng chấn thương khó chịu.");
                else
                    AddExercise(canonicalExercises, "Seated Cable Biceps Curl (Cuốn tay trước cáp ngồi)", "2 hiệp x 12-15 lần", "Ngồi tựa lưng và cô lập thân trên, không truyền lực xuống chi bị chấn thương.");
            }
            else if (hasBack || hasHernia)
            {
                AddExercise(canonicalExercises, "Chest-Supported Dumbbell Row (Kéo tạ đơn ngực tựa ghế dốc)", "3 hiệp x 10-12 lần", "Tựa ngực chắc chắn vào đệm dốc để triệt tiêu hoàn toàn lực nén ép dọc trục lên thắt lưng L4-L5.");
                AddExercise(canonicalExercises, "Seated Cable Row (Kéo cáp ngồi thẳng có đệm chân)", "3 hiệp x 10-12 lần", "Giữ cột sống trung tính, kéo khuỷu tay sát sườn, siết chặt cơ xô lưng.");
                AddExercise(canonicalExercises, "Seated Leg Curl Machine (Máy cuốn đùi sau có tựa)", "3 hiệp x 10-12 lần", "Giữ lưng tựa sát đệm, không tạo tải nén dọc trục lên cột sống.");
                AddExercise(canonicalExercises, "Cable Face Pull (Kéo cáp ngang mặt)", "3 hiệp x 12-15 lần", "Kéo về ngang mặt, giữ vai hạ thấp và không ưỡn lưng.");
                AddExercise(canonicalExercises, "Dead Bug (Tập cơ lõi chống xoay)", "3 hiệp x 10 lần mỗi bên", "Duy trì áp lực lưng dưới chạm sát mặt sàn, kích hoạt cơ bụng sâu bảo vệ đĩa đệm.");
            }
            else if (hasKnee)
            {
                AddExercise(canonicalExercises, "Seated Leg Curl Machine (Máy cuốn đùi sau)", "3 hiệp x 10-12 lần", "Dùng biên độ không đau, không khóa khớp gối.");
                AddExercise(canonicalExercises, "Hip Thrust (Đẩy hông có kiểm soát)", "3 hiệp x 10-12 lần", "Giữ cẳng chân gần vuông góc và dừng nếu xuất hiện đau gối.");
                AddExercise(canonicalExercises, "Machine Chest Press (Máy đẩy ngực có tựa)", "3 hiệp x 10-12 lần", "Lưng tựa chắc chắn, thở liên tục và kiểm soát biên độ.");
                AddExercise(canonicalExercises, "Seated Cable Row (Kéo cáp ngồi)", "3 hiệp x 10-12 lần", "Giữ cột sống trung tính và không dùng lực giật.");
            }
            else
            {
                AddExercise(canonicalExercises, "Machine Chest Press (Máy đẩy ngực có tựa)", "3 hiệp x 10-12 lần", "Quỹ đạo máy khối cố định, an toàn cho khớp vai và cổ tay.");
                AddExercise(canonicalExercises, "Lat Pulldown (Kéo xô máy cáp trước mặt)", "3 hiệp x 10-12 lần", "Kéo thanh đòn về phía xương quai xanh, siết cơ xô lưng có kiểm soát.");
                AddExercise(canonicalExercises, "Leg Press 45 độ (Máy đạp đùi)", "3 hiệp x 10-12 lần", "Lưng tựa chắc chắn vào ghế đệm, đạp bằng gót chân, không khóa khớp gối.");
                AddExercise(canonicalExercises, "Plank (Chống khuỷu tay giữ tĩnh)", "3 hiệp x 30-45 giây", "Siết chặt cơ bụng và cơ mông, giữ thân người thành một đường thẳng.");
            }
        }

        // Giới hạn số bài tập tối đa 6
        while (canonicalExercises.Count > 6)
        {
            canonicalExercises.RemoveAt(canonicalExercises.Count - 1);
        }

        // Kiểm tra an toàn y tế đối với các bài tập (Safety Violations Check)
        var exerciseNames = canonicalExercises
            .Select(e => e?["name"]?.GetValue<string>() ?? string.Empty)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        var violations = AiWorkoutSafetyPolicy.FindExerciseViolations(safety, exerciseNames).ToList();
        foreach (var name in exerciseNames)
        {
            var norm = name.ToLowerInvariant();
            if (hasBack && ContainsAny(norm, AxialBackBannedExercises))
            {
                violations.Add($"'{name}' tạo lực nén ép dọc trục nguy hiểm cho cột sống/đĩa đệm.");
            }
            if (hasSevereInjury && ContainsAny(norm, SevereInjuryBannedExercises))
            {
                violations.Add($"'{name}' tạo áp lực lên chi bị chấn thương nặng; chỉ được tập cô lập vùng an toàn.");
            }
        }

        if (violations.Count > 0)
        {
            var ex = new InvalidOperationException("AI sinh bài tập xung đột với hồ sơ sức khỏe: " + string.Join(" ", violations));
            ex.Data["StatusCode"] = 422;
            ex.Data["Code"] = "AI_SAFETY_CHECK_FAILED";
            ex.Data["ProposedHandling"] = "Không sử dụng kết quả này; kiểm tra hồ sơ sức khỏe và tạo lại gợi ý.";
            throw ex;
        }

        // 5. Đóng gói kết quả đầu ra đầy đủ
        var finalResult = new JsonObject
        {
            // Định dạng mới nhất theo yêu cầu NLP Prompt của HLV
            ["extracted_keywords"] = JsonSerializer.SerializeToNode(detectedKeywords),
            ["ai_reasoning"] = aiReasoning,
            ["protocol"] = new JsonObject
            {
                ["frequency"] = frequency,
                ["nutrition"] = nutrition
            },
            ["exercises"] = canonicalExercises,

            // Thuộc tính tương thích ngược cho giao diện và hệ thống lưu giáo án
            ["title"] = hasSevereInjury
                ? "Phác đồ Phục hồi & Tập cô lập vùng an toàn"
                : hasBack
                    ? "Phác đồ Bảo vệ Cột sống & Phát triển Thể lực"
                    : "Phác đồ Huấn luyện Thể thao Cá nhân hóa",
            ["targetAudience"] = $"Học viên BMI {safety.Bmi:0.0}, trình độ {safety.EffectiveLevel}; mục tiêu: {safety.EffectiveGoal}.",
            ["clinicalAssessment"] = aiReasoning,
            ["weeklyVolume"] = frequency,
            ["heartRateZone"] = hasSevereInjury || safety.IsSeverelyUnderweight
                ? "Zone 1-2 • ưu tiên phục hồi và khả năng dung nạp"
                : hasHypertension
                    ? "Zone 1-2 (dưới 75% HRmax)"
                    : safety.IsUnderweight
                        ? "Zone 2 (60-70% HRmax) • không HIIT"
                        : safety.WantsEndurance && !safety.EffectiveLevel.Equals("Beginner", StringComparison.OrdinalIgnoreCase)
                            ? "Zone 2 (60-70% HRmax) xây nền • 1 buổi/tuần interval Zone 3-4 (70-85% HRmax)"
                            : "Zone 2 (60-70% HRmax)",
            ["nutritionGuidance"] = nutrition,
            ["coachReviewNote"] = aiReasoning,
            ["aiReasoning"] = aiReasoning,
            ["recommendedVolume"] = frequency,
            ["conflictWarnings"] = JsonSerializer.SerializeToNode(safety.ConflictWarnings),
            ["mandatoryAdjustments"] = JsonSerializer.SerializeToNode(safety.MandatoryAdjustments),
            ["coachNotes"] = new JsonArray { "Khởi động kỹ khớp trước buổi tập và thực hiện bài tập theo đúng hướng dẫn kỹ thuật." },
            ["extractedTags"] = JsonSerializer.SerializeToNode(detectedKeywords)
        };

        return finalResult;
    }

    private static void AddExercise(JsonArray arr, string name, string volume, string focus)
    {
        var (sets, reps) = SplitVolume(volume);
        arr.Add(new JsonObject
        {
            ["name"] = name,
            ["sets_reps"] = volume,
            ["medical_focus"] = focus,
            ["sets"] = sets,
            ["reps"] = reps,
            ["volume"] = volume,
            ["note"] = focus
        });
    }

    private static (string Sets, string Reps) SplitVolume(string volume)
    {
        var match = Regex.Match(volume.Trim(),
            @"^(?<sets>\d+(?:\s*[-–]\s*\d+)?)\s*(?:sets?|hiệp)?\s*[x×*]\s*(?<reps>.+)$",
            RegexOptions.IgnoreCase);
        return match.Success
            ? (match.Groups["sets"].Value.Trim(), match.Groups["reps"].Value.Trim())
            : ("3", volume.Trim());
    }

    private static string ExtractJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "{}";
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNl = trimmed.IndexOf('\n');
            if (firstNl >= 0) trimmed = trimmed[(firstNl + 1)..];
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0) trimmed = trimmed[..lastFence];
            trimmed = trimmed.Trim();
        }
        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
        }
        return trimmed;
    }

    private static string? GetStringCoerced(JsonObject obj, params string[] candidateKeys)
    {
        foreach (var key in candidateKeys)
        {
            if (obj.TryGetPropertyValue(key, out var node) && node is not null)
            {
                if (node is JsonValue val && val.TryGetValue<string>(out var str) && !string.IsNullOrWhiteSpace(str))
                    return str.Trim();
                var s = node.ToString();
                if (!string.IsNullOrWhiteSpace(s) && s != "null") return s.Trim();
            }
            var matched = obj.FirstOrDefault(kvp =>
                string.Equals(kvp.Key.Replace("_", ""), key.Replace("_", ""), StringComparison.OrdinalIgnoreCase));
            if (matched.Value is not null)
            {
                if (matched.Value is JsonValue val && val.TryGetValue<string>(out var str) && !string.IsNullOrWhiteSpace(str))
                    return str.Trim();
                var s = matched.Value.ToString();
                if (!string.IsNullOrWhiteSpace(s) && s != "null") return s.Trim();
            }
        }
        return null;
    }

    private static JsonArray? FindExercisesArray(JsonObject obj)
    {
        string[] candidates = ["exercises", "workoutplan", "exerciselist", "items", "plan"];
        foreach (var candidate in candidates)
        {
            if (obj.TryGetPropertyValue(candidate, out var node) && node is JsonArray arr)
                return arr;

            var matched = obj.FirstOrDefault(kvp =>
                string.Equals(kvp.Key.Replace("_", ""), candidate, StringComparison.OrdinalIgnoreCase));
            if (matched.Value is JsonArray matchedArr)
                return matchedArr;
        }
        return null;
    }

    private static bool ContainsAny(string text, string[] terms) =>
        terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string Normalize(string tag) =>
        new string(tag.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string EnsureEmoji(string tag)
    {
        var trimmed = tag.Trim();
        if (string.IsNullOrEmpty(trimmed)) return trimmed;
        if (char.IsLetterOrDigit(trimmed[0]))
        {
            var lower = trimmed.ToLowerInvariant();
            if (lower.Contains("chấn thương") || lower.Contains("thoát vị") || lower.Contains("gãy")) return "⚠️ " + trimmed;
            if (lower.Contains("lưng") || lower.Contains("cột sống")) return "🦴 " + trimmed;
            if (lower.Contains("gối")) return "🦵 " + trimmed;
            if (lower.Contains("vai") || lower.Contains("tay")) return "💪 " + trimmed;
            if (lower.Contains("tim") || lower.Contains("huyết áp")) return "❤️ " + trimmed;
            if (lower.Contains("cơ") || lower.Contains("mục tiêu")) return "🎯 " + trimmed;
            if (lower.Contains("mỡ")) return "🔥 " + trimmed;
            if (lower.Contains("máy")) return "🪑 " + trimmed;
            return "📌 " + trimmed;
        }
        return trimmed;
    }
}
