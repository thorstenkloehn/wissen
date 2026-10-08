using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wissen.Infrastructure.Backup;
using Wissen.Infrastructure.Data;

namespace Wissen.Infrastructure;

public static class DependencyInjection
{
    // Gemeinsame Verdrahtung des Datenzugriffs für Web-App und Konsolenanwendung.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddSingleton(new DatabaseBackup(connectionString));
        services.AddScoped<SeitenXmlExport>();
        return services;
    }
}
