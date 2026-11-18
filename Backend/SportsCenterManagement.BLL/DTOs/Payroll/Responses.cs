using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Payroll;



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

