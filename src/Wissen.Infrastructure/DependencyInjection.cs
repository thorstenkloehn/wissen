using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wissen.Infrastructure.Backup;
using Wissen.Infrastructure.Data;
using Wissen.Infrastructure.Identity;
using Wissen.Infrastructure.Rendering;

namespace Wissen.Infrastructure;

public static class DependencyInjection
{
    // Gemeinsame Verdrahtung des Datenzugriffs für Web-App und Konsolenanwendung.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));
        // Die Identity-Oberfläche der Web-App begrenzt Schlüsselspalten auf 128 Zeichen, und so sind die
        // Migrationen erzeugt. Ohne dieselbe Einstellung sähe die Konsolenanwendung ein abweichendes Modell,
        // und migrate bräche mit "pending model changes" ab.
        services.Configure<IdentityOptions>(options =>
        {
            options.Stores.MaxLengthForKeys = 128;
            options.Password.RequiredLength = Passwortregeln.Mindestlaenge;
        });
        services.AddSingleton(new DatabaseBackup(connectionString));
        services.AddScoped<SeitenXmlExport>();
        services.AddScoped<SeitenXmlImport>();
        services.AddScoped<SeitenRendern>();
        services.AddScoped<KontoAnlage>();
        return services;
    }
}
