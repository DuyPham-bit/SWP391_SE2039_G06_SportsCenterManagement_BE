using Microsoft.Extensions.DependencyInjection;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.BLL.Services;

namespace SportsCenterManagement.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLayer(this IServiceCollection services)
    {
        // Core & Authentication Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IAccessControlService, AccessControlService>();
        services.AddScoped<ICenterAccessService, CenterAccessService>();
        services.AddScoped<ICenterScopeService, CenterScopeService>();
        services.AddScoped<IStaffManagementService, StaffManagementService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<ICenterNotificationService, CenterNotificationService>();
        services.AddScoped<IRolePermissionService, RolePermissionService>();

        // Classes, Catalogs & Schedules
        services.AddScoped<IClassService, ClassService>();
        services.AddScoped<IClassManagementService, ClassManagementService>();
        services.AddScoped<IClassEnrollmentService, ClassEnrollmentService>();
        services.AddScoped<ISportManagementService, SportManagementService>();
        services.AddScoped<ICoachAssignmentQueryService, CoachAssignmentQueryService>();
        services.AddScoped<IClassScheduleQueryService, ClassScheduleQueryService>();
        services.AddScoped<ISessionBookingService, SessionBookingService>();
        services.AddScoped<ICoreFlowService, CoreFlowService>();
        services.AddScoped<IPublicCatalogService, PublicCatalogService>();

        // Membership & Operations
        services.AddScoped<IMembershipPackageService, MembershipPackageService>();
        services.AddScoped<IMemberService, MemberService>();
        services.AddScoped<ICheckinService, CheckinService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IPaymentService, PaymentService>();

        // Extended domain services
        services.AddScoped<SupportRequestService>();
        services.AddScoped<TrainingService>();
        services.AddScoped<CoachProfessionalService>();
        services.AddScoped<PayrollService>();
        services.AddScoped<MemberAiContextService>();
        services.AddScoped<ClassReminderService>();

        return services;
    }
}
