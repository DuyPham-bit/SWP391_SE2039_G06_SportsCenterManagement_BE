using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Exceptions;
using SportsCenterManagement.BLL.Services;

// Invalid public query inputs must fail with a client error before accessing the database.
var service = new ClassScheduleQueryService(null!);
var cases = new (string Name, SessionScheduleQuery Query)[] {
    ("page zero", new(null, null, null, null, Page: 0)),
    ("page too large", new(null, null, null, null, Page: 1_000_001)),
    ("page size too large", new(null, null, null, null, PageSize: 101)),
    ("negative sport", new(-1, null, null, null)),
    ("negative coach", new(null, -1, null, null)),
    ("negative center", new(null, null, null, null, CenterId: -1)),
    ("reversed dates", new(null, null, new(2026,10,11), new(2026,10,10))),
    ("overlong date range", new(null, null, new(2026,1,1), new(2026,12,31))),
    ("maximum date", new(null, null, DateOnly.MaxValue, null)),
    ("default date range overflow", new(null, null, DateOnly.MaxValue.AddDays(-1), null))
};
foreach (var (name, query) in cases) {
    try { await service.GetSessionsAsync(query); throw new Exception($"FAIL: {name}: accepted invalid query"); }
    catch (FlowException ex) when (ex.StatusCode == 400) { Console.WriteLine($"PASS: {name}"); }
}
Console.WriteLine($"Passed {cases.Length} backend input regression checks.");

foreach (var role in new[] { "Member", "Coach", "Receptionist" }) {
    var capabilities = CenterRolePolicy.Capabilities(role);
    if (capabilities.Contains("manage_roles") || capabilities.Contains("manage_staff"))
        throw new Exception($"FAIL: {role} can escalate to administrative capability");
    Console.WriteLine($"PASS: {role} delegation boundary");
}
if (CenterRolePolicy.Key(1, 3) == CenterRolePolicy.Key(2, 3)) throw new Exception("FAIL: center policy key collision");
Console.WriteLine("PASS: center policies isolated by center and role");
