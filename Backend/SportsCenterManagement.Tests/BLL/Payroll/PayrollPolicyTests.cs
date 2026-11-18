namespace SportsCenterManagement.Tests.BLL.Payroll;

public class PayrollPolicyTests
{
    private static readonly DateOnly Dec1 = new(2026, 12, 1);

    [Fact]
    public void Supersede_FuturePolicy_KeepsCurrentPolicyActiveUntilDayBefore()
    {
        var (to, status) = PayrollService.Supersede(new DateOnly(2026, 1, 1), null, Dec1);

        Assert.Equal("Active", status);
        Assert.Equal(new DateOnly(2026, 11, 30), to);
    }

    [Fact]
    public void Supersede_PolicyStartingOnOrAfterNewDate_IsArchived()
    {
        Assert.Equal("Archived", PayrollService.Supersede(Dec1, null, Dec1).Status);
        Assert.Equal("Archived", PayrollService.Supersede(Dec1.AddMonths(1), null, Dec1).Status);
    }

    [Fact]
    public void Supersede_EarlierEndDate_IsNotExtended()
    {
        var end = new DateOnly(2026, 6, 30);

        Assert.Equal(end, PayrollService.Supersede(new DateOnly(2026, 1, 1), end, Dec1).EffectiveTo);
    }
}
