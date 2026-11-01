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

public sealed record PayrollRow(
    long UserId,
    string EmployeeCode,
    string FullName,
    string Role,
    long? CenterId,
    string? CenterName,
    decimal BaseSalary,
    decimal Allowance,
    int CompletedSessions,
    decimal SessionBonus,
    decimal EstimatedTotal,
    bool PolicyConfigured,
    decimal? CommissionPerSession = null,
    int? TargetSessions = null,
    DateOnly? EffectiveFrom = null,
    DateOnly? EffectiveTo = null);

public sealed record PayrollReport(
    int Year,
    int Month,
    decimal TotalBaseSalary,
    decimal TotalVariablePay,
    decimal EstimatedPayroll,
    IReadOnlyList<PayrollRow> Employees);
