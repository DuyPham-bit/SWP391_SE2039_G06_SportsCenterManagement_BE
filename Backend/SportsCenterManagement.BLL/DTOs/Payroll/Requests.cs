using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Payroll;

public sealed record SaveStaffCompensationRequest(
    [Range(1, long.MaxValue)] long UserId,
    [Range(typeof(decimal), "0", "1000000000")] decimal MonthlyBaseSalary,
    [Range(typeof(decimal), "0", "1000000000")] decimal MonthlyAllowance = 0m,
    DateOnly EffectiveFrom = default,
    DateOnly? EffectiveTo = null,
    [Range(typeof(decimal), "0", "100000000")] decimal? CommissionPerSession = null,
    [Range(0, 10000)] int? TargetSessions = null);

