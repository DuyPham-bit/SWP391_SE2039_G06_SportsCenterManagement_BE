using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Repositories.Implementations;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.DAL;

public static class DependencyInjection
{
    public static IServiceCollection AddDal(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SportsCenter")
            ?? throw new InvalidOperationException("Connection string 'SportsCenter' is not configured.");

        services.AddDbContext<SportsCenterDbContext>(options =>
            options.UseSqlServer(connectionString)
                   .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IClassRepository, ClassRepository>();

        return services;
    }
}
