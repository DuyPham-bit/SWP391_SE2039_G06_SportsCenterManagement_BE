namespace SportsCenterManagement.DAL.Authorization;

public static class PermissionCodes
{
    public const string MemberSelfRead = "members.self.read";
    public const string MemberSelfUpdate = "members.self.update";
    public const string MemberCenterRead = "members.center.read";
    public const string MemberCenterCreate = "members.center.create";
    public const string MemberCenterUpdate = "members.center.update";
    public const string MemberCenterManageStatus = "members.center.manage-status";
    public const string StaffCenterRead = "staff.center.read";
    public const string StaffCenterCreate = "staff.center.create";
    public const string StaffCenterUpdate = "staff.center.update";
    public const string MembershipPackagesManage = "membership-packages.manage";
    public const string SubscriptionSelfCreate = "subscriptions.self.create";
    public const string SubscriptionCenterCreate = "subscriptions.center.create";
    public const string SubscriptionRead = "subscriptions.read";
    public const string PaymentSelfCreate = "payments.self.create";
    public const string PaymentCenterCash = "payments.center.cash";
    public const string AuditCenterRead = "audit.center.read";
    public const string RolePermissionManage = "roles.permissions.manage";
}

public static class RoleNames
{
    public const string Member = "Member";
    public const string Coach = "Coach";
    public const string Receptionist = "Receptionist";
    public const string Manager = "Manager";
    public const string SystemAdmin = "SystemAdmin";
}
