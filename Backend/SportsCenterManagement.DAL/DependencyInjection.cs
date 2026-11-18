using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.DAL;

public static class DependencyInjection
{
    /// <summary>Registers the DbContext and the repositories / unit of work.</summary>
    public static IServiceCollection AddDataAccess(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SportsCenterDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IClassRepository, ClassRepository>();

        return services;
    }
}
