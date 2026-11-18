using Microsoft.Extensions.DependencyInjection;
using SportsCenterManagement.DAL;

namespace SportsCenterManagement.BLL;

public static class DependencyInjection
{
    /// <summary>Registers the business services and, through <see cref="SportsCenterManagement.DAL.DependencyInjection.AddDataAccess"/>, the data layer.</summary>
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services, string connectionString)
    {
        services.AddDataAccess(connectionString);
        services.AddScoped<CenterAccessGuard>();
        services.AddScoped<SportsCenterManagement.BLL.Common.SystemHealthService>();

        // Layer 2: BLL (Business Logic Services)
        // Flow 1 Services
        services.AddScoped<IClassService, ClassService>();
        services.AddScoped<IMembershipPackageService, MembershipPackageService>();
        services.AddScoped<ICoreFlowService, CoreFlowService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ICenterScopeService, CenterScopeService>();
        services.AddScoped<IClassEnrollmentService, ClassEnrollmentService>();
        services.AddScoped<IMemberEnrollmentOptionsService, MemberEnrollmentOptionsService>();
        services.AddScoped<IPublicCatalogService, PublicCatalogService>();
        services.AddScoped<IMemberService, MemberService>();
        services.AddScoped<IMemberActivationService, MemberActivationService>();
        services.AddSingleton<SportsCenterManagement.BLL.Common.MediaUrlBuilder>();
        services.AddScoped<IAccessControlService, AccessControlService>();
        services.AddScoped<IStaffManagementService, StaffManagementService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<ICenterNotificationService, CenterNotificationService>();

        // Flow 2 Services
        services.AddScoped<IClassManagementService, ClassManagementService>();
        services.AddScoped<ISportManagementService, SportManagementService>();
        services.AddScoped<ICoachAssignmentQueryService, CoachAssignmentQueryService>();
        services.AddScoped<IClassScheduleQueryService, ClassScheduleQueryService>();
        services.AddScoped<ISessionBookingService, SessionBookingService>();
        services.AddScoped<IRolePermissionService, RolePermissionService>();

        // Flow 3 Services
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ICheckinService, CheckinService>();
        services.AddScoped<SupportRequestService>();
        services.AddScoped<TrainingService>();
        services.AddScoped<CoachProfessionalService>();
        services.AddScoped<PayrollService>();
        services.AddScoped<MemberAiContextService>();
        services.AddSingleton<IBusinessClock, BusinessClock>();
        services.AddScoped<ClassReminderService>();
        services.AddScoped<LandingContentService>();
        services.AddScoped<CampusMapService>();

        return services;
    }
}
