using Application.Abstractions.Data;
using Application.Abstractions.Persistence;
using Application.Abstractions.Services;
using Infrastructure.Database;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database is not configured.");

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // Unit-of-work + write-side repositories share the same scoped DbContext.
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IBillRepository, BillRepository>();
        services.AddScoped<IVendorRepository, VendorRepository>();
        services.AddScoped<IHeadTransactionRepository, HeadTransactionRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IPlanFeatureLookup, PlanFeatureLookup>();
        services.AddScoped<IPlanPolicy, PlanPolicy>();
        services.AddScoped<IBusinessClock, BusinessClock>();
        services.AddScoped<IBillNumberGenerator, BillNumberGenerator>();
        services.AddScoped<IActivityLogger, ActivityLogger>();
        services.AddScoped<IAccountingTypeProvider, AccountingTypeProvider>();

        return services;
    }
}
