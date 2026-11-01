using System.Text.Json;
using System.Text.Json.Nodes;

namespace SportsCenterManagement.BLL.Services;

public static class AiWorkoutRecommendationProcessor
{
    private static readonly string[] RequiredTextFields =
    [
        "title", "targetAudience", "clinicalAssessment", "weeklyVolume", "heartRateZone",
        "nutritionGuidance", "coachReviewNote"
    ];

    private static readonly string[] RequiredListFields =
    [
        "conflictWarnings", "mandatoryAdjustments", "coachNotes"
    ];

    private static readonly string[] RequiredExerciseFields =
    [
        "name", "sets", "reps", "note"
    ];

    public static JsonObject Process(string reply, AiWorkoutSafetyProfile safety, decimal weightKg)
    {
        var result = ParseAndValidate(reply, safety);
        var exercises = (JsonArray)result["exercises"]!;
        var violations = AiWorkoutSafetyPolicy.FindExerciseViolations(safety,
            exercises.Select(exercise => exercise!["name"]!.GetValue<string>()));

        if (violations.Length > 0)
            throw TaggedFailure(
                "AI sinh bài tập xung đột với hồ sơ sức khỏe: " + string.Join(" ", violations),
                422,
                "AI_SAFETY_CHECK_FAILED",
                "Không sử dụng kết quả này; kiểm tra hồ sơ sức khỏe và tạo lại gợi ý.");

        ApplyDeterministicOverrides(result, safety, weightKg);
        return result;
    }

    private static JsonObject ParseAndValidate(string reply, AiWorkoutSafetyProfile safety)
    {
        try
        {
            var cleaned = (reply ?? string.Empty).Trim();
            if (cleaned.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned[7..];
            else if (cleaned.StartsWith("```"))
                cleaned = cleaned[3..];
            if (cleaned.EndsWith("```"))
                cleaned = cleaned[..^3];
            cleaned = cleaned.Trim();

            var firstBrace = cleaned.IndexOf('{');
            var lastBrace = cleaned.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                cleaned = cleaned[firstBrace..(lastBrace + 1)];
            }

            if (JsonNode.Parse(cleaned) is not JsonObject result) throw new JsonException();

            // Locate exercises array
            JsonArray? exercises = null;
            if (result["exercises"] is JsonArray directArr)
            {
                exercises = directArr;
            }
            else if (result["workout"]?["exercises"] is JsonArray workoutArr)
            {
                exercises = workoutArr;
                result["exercises"] = exercises;
            }
            else if (result["plan"]?["exercises"] is JsonArray planArr)
            {
                exercises = planArr;
                result["exercises"] = exercises;
            }

            if (exercises == null || exercises.Count == 0) throw new JsonException();

            // Normalize each exercise in array
            var sanitizedExercises = new JsonArray();
            foreach (var itemNode in exercises)
            {
                if (itemNode is not JsonObject exObj) continue;
                var name = GetStringValue(exObj["name"]);
                if (string.IsNullOrWhiteSpace(name)) continue;

                var sets = GetStringValue(exObj["sets"]);
                if (string.IsNullOrWhiteSpace(sets)) sets = "3";

                var reps = GetStringValue(exObj["reps"]);
                if (string.IsNullOrWhiteSpace(reps)) reps = "10-12";

                var note = GetStringValue(exObj["note"]);
                if (string.IsNullOrWhiteSpace(note)) note = "Kiểm soát nhịp thở và biên độ chuyển động";

                sanitizedExercises.Add(new JsonObject
                {
                    ["name"] = name.Trim(),
                    ["sets"] = sets.Trim(),
                    ["reps"] = reps.Trim(),
                    ["note"] = note.Trim()
                });
            }

            if (sanitizedExercises.Count < 3) throw new JsonException();

            if (sanitizedExercises.Count == 3)
            {
                sanitizedExercises.Add(new JsonObject
                {
                    ["name"] = safety.HasBackIssue ? "Deadbug - Ổn định cơ lõi" : "Deadbug Core Stabilization",
                    ["sets"] = "3",
                    ["reps"] = "10 lần/bên",
                    ["note"] = "Nằm ngửa, giữ thắt lưng sát sàn, chuyển động có kiểm soát"
                });
            }

            while (sanitizedExercises.Count > 6)
            {
                sanitizedExercises.RemoveAt(sanitizedExercises.Count - 1);
            }

            result["exercises"] = sanitizedExercises;

            EnsureString(result, "title", "Giáo án Thể lực & Sức khỏe Cá nhân hóa");
            EnsureString(result, "targetAudience", $"Học viên BMI {safety.Bmi:0.0}, trình độ {safety.EffectiveLevel}; mục tiêu: {safety.EffectiveGoal}");
            EnsureString(result, "clinicalAssessment", "Đã phân tích hồ sơ sinh trắc học và kiểm tra an toàn y học thể thao.");
            EnsureString(result, "weeklyVolume", "2-3 buổi/tuần • 45-60 phút/buổi");
            EnsureString(result, "heartRateZone", "Zone 2 (60-70% HRmax) • giữ nhịp tim ổn định");
            EnsureString(result, "nutritionGuidance", "Dinh dưỡng cân bằng, đủ protein và bù nước trong buổi tập.");
            EnsureString(result, "coachReviewNote", "HLV cần giám sát và điều chỉnh biên độ/tải trọng phù hợp phản ứng thực tế.");
            EnsureString(result, "aiReasoning", safety.AiReasoning ?? "Hệ thống AI đã đọc hiểu và điều chỉnh giáo án theo thể trạng.");

            EnsureStringArray(result, "conflictWarnings", safety.ConflictWarnings);
            EnsureStringArray(result, "mandatoryAdjustments", safety.MandatoryAdjustments);
            EnsureStringArray(result, "coachNotes", ["Theo dõi nhịp thở và cảm nhận cơ trong suốt buổi tập."]);

            return result;
        }
        catch (JsonException)
        {
            throw TaggedFailure(
                "AI trả giáo án không đúng định dạng. Vui lòng thử lại.",
                502,
                "AI_INVALID_RESPONSE",
                "Tạo lại giáo án; nếu lỗi lặp lại, kiểm tra cấu hình và phiên bản mô hình AI.");
        }
    }

    private static void ApplyDeterministicOverrides(JsonObject result, AiWorkoutSafetyProfile safety, decimal weightKg)
    {
        result["conflictWarnings"] = JsonSerializer.SerializeToNode(safety.ConflictWarnings);
        result["mandatoryAdjustments"] = JsonSerializer.SerializeToNode(safety.MandatoryAdjustments);

        var bmiStatus = safety.IsSeverelyUnderweight ? "thiếu cân nghiêm trọng"
            : safety.IsUnderweight ? "thiếu cân"
            : safety.IsOverweight ? "thừa cân"
            : "bình thường";
        var hasMedicalRestriction = safety.HasBackIssue || safety.HasWristOrShoulderIssue ||
            safety.HasKneeIssue || safety.HasHypertension || safety.HasSevereLegInjury ||
            (safety.MedicalConditions != null && safety.MedicalConditions.Length > 0);
        result["targetAudience"] =
            $"Học viên BMI {safety.Bmi:0.0} ({bmiStatus}), trình độ {safety.EffectiveLevel}; " +
            $"mục tiêu hiệu lực: {safety.EffectiveGoal}.";
        result["clinicalAssessment"] =
            $"BMI {safety.Bmi:0.0} được dùng làm dữ liệu thể trạng ưu tiên. " +
            (hasMedicalRestriction
                ? "Có lưu ý sức khỏe/chấn thương cần áp dụng các điều chỉnh an toàn bắt buộc."
                : "Chưa phát hiện hạn chế y khoa trong dữ liệu được cung cấp; tiếp tục theo dõi phản ứng khi tập.");

        if (safety.WantsMuscleGain && safety.WantsFatLoss)
            result["title"] = safety.WantsEndurance
                ? "Phác đồ Tái cấu trúc Cơ thể & Phát triển Sức bền"
                : "Phác đồ Tái cấu trúc Cơ thể";

        if (safety.IsUnderweight)
        {
            result["weeklyVolume"] = safety.IsSeverelyUnderweight
                ? "2-3 buổi/tuần • 35-45 phút/buổi • cường độ thấp đến vừa"
                : "2-3 buổi/tuần • 40-55 phút/buổi • cường độ vừa";
            result["heartRateZone"] = safety.IsSeverelyUnderweight
                ? "Zone 1-2 • ưu tiên khả năng dung nạp và hồi phục"
                : "Zone 2 • tránh HIIT và khối lượng gây kiệt sức";
            var minimumProtein = Math.Round(weightKg * 1.6m);
            var maximumProtein = Math.Round(weightKg * 2m);
            result["nutritionGuidance"] =
                $"Thặng dư 300-500 kcal/ngày; protein tham khảo {minimumProtein:0}-{maximumProtein:0} g/ngày " +
                "(1,6-2,0 g/kg), chia đều các bữa và theo dõi khả năng dung nạp. " +
                "Điều chỉnh với chuyên gia dinh dưỡng nếu có bệnh nền.";
        }
        else if (safety.IsOverweight)
        {
            result["nutritionGuidance"] =
                "Thâm hụt 300-500 kcal/ngày; ưu tiên protein khoảng 1,6-2,0 g/kg/ngày, thực phẩm giàu chất xơ " +
                "và đủ nước. Điều chỉnh theo bệnh nền và đánh giá của chuyên gia dinh dưỡng.";
        }
        else if (safety.WantsMuscleGain && safety.WantsFatLoss)
        {
            var minimumProtein = Math.Round(weightKg * 1.6m);
            var maximumProtein = Math.Round(weightKg * 2.2m);
            result["nutritionGuidance"] =
                $"Duy trì năng lượng hoặc thâm hụt nhẹ 100-250 kcal/ngày; protein {minimumProtein:0}-{maximumProtein:0} g/ngày " +
                "(1,6-2,2 g/kg), ưu tiên thực phẩm ít chế biến và điều chỉnh theo xu hướng vòng eo, sức mạnh và cân nặng.";
        }
        else if (safety.WantsFatLoss)
        {
            result["nutritionGuidance"] =
                "Thâm hụt 250-400 kcal/ngày, protein 1,6-2,2 g/kg/ngày và theo dõi sức mạnh để hạn chế mất khối lượng nạc.";
        }
        else if (safety.WantsMuscleGain)
        {
            result["nutritionGuidance"] =
                "Thặng dư nhẹ 150-300 kcal/ngày, protein 1,6-2,0 g/kg/ngày; chỉ tăng năng lượng khi cân nặng và sức mạnh không tiến triển.";
        }
        else
        {
            result["nutritionGuidance"] =
                "Duy trì năng lượng, protein 1,4-1,8 g/kg/ngày và phân bổ carbohydrate quanh buổi tập để hỗ trợ hiệu suất.";
        }

        if (safety.WantsEndurance && !safety.IsUnderweight && !safety.HasHypertension)
            result["heartRateZone"] = safety.EffectiveLevel.Equals("Beginner", StringComparison.OrdinalIgnoreCase)
                ? "Zone 2 (60-70% HRmax) chủ đạo • thêm các đoạn Zone 3 ngắn khi đã dung nạp tốt"
                : "Zone 2 (60-70% HRmax) xây nền • 1 buổi/tuần interval Zone 3-4 (70-85% HRmax) để phát triển sức bền";

        if (safety.HasHypertension)
            result["heartRateZone"] =
                "Zone 1-2 • không vượt 75% HRmax • dừng tập khi chóng mặt, đau ngực hoặc khó thở bất thường";

        if (safety.HasSevereLegInjury)
        {
            result["title"] = "Phác đồ Phục hồi Chức năng & Tập Cô lập Thân trên Tại chỗ";
            result["weeklyVolume"] = "2-3 buổi/tuần • 30-45 phút/buổi • hoàn toàn ở tư thế ngồi có tựa";
            result["heartRateZone"] = "Zone 1-2 (50-65% HRmax) • giữ nhịp tim ổn định và thúc đẩy tuần hoàn";
        }

        if (result["extractedTags"] is not JsonArray extractedArr || extractedArr.Count == 0)
        {
            var arr = new JsonArray();
            if (safety.ExtractedTags != null && safety.ExtractedTags.Length > 0)
            {
                foreach (var t in safety.ExtractedTags) arr.Add(t);
            }
            result["extractedTags"] = arr;
        }

        if (result["aiReasoning"] is not JsonValue || string.IsNullOrWhiteSpace(result["aiReasoning"]?.GetValue<string>()))
        {
            result["aiReasoning"] = safety.AiReasoning ?? result["clinicalAssessment"]?.GetValue<string>() ?? "";
        }

        if (result["recommendedVolume"] is null)
        {
            result["recommendedVolume"] = $"{result["weeklyVolume"]} | Nhịp tim: {result["heartRateZone"]}";
        }
    }

    private static string GetStringValue(JsonNode? node)
    {
        if (node is null) return string.Empty;
        if (node is JsonValue val)
        {
            if (val.TryGetValue<string>(out var s)) return s;
            return val.ToString();
        }
        return node.ToString();
    }

    private static void EnsureString(JsonObject obj, string key, string fallback)
    {
        var existing = GetStringValue(obj[key]);
        obj[key] = string.IsNullOrWhiteSpace(existing) ? fallback : existing.Trim();
    }

    private static void EnsureStringArray(JsonObject obj, string key, IEnumerable<string> fallback)
    {
        if (obj[key] is not JsonArray array || array.Count == 0)
        {
            var newArr = new JsonArray();
            foreach (var item in fallback)
            {
                if (!string.IsNullOrWhiteSpace(item)) newArr.Add(item.Trim());
            }
            obj[key] = newArr;
        }
    }

    private static InvalidOperationException TaggedFailure(
        string message,
        int statusCode,
        string code,
        string proposedHandling)
    {
        var exception = new InvalidOperationException(message);
        exception.Data["StatusCode"] = statusCode;
        exception.Data["Code"] = code;
        exception.Data["ProposedHandling"] = proposedHandling;
        return exception;
    }
}
