using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wissen.Cli;
using Wissen.Cli.Modules;
using Wissen.Infrastructure;
using Wissen.Infrastructure.Data;
using Wissen.Infrastructure.Identity;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

try
{
    // appsettings.json liegt neben der ausführbaren Datei, nicht im aktuellen Verzeichnis.
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory });
    builder.Configuration["Logging:LogLevel:Default"] = "Warning";
    builder.Services.AddInfrastructure(builder.Configuration);
    // Für konto-anlegen und konto-admin: dieselben Passwortregeln und Fehlermeldungen wie in der Web-App.
    builder.Services.AddIdentityCore<IdentityUser>()
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddErrorDescriber<GermanIdentityErrorDescriber>();

    // Module der Konsolenanwendung; neue Module hier anmelden.
    builder.Services.AddScoped<ICommandModule, BackupModule>();
    builder.Services.AddScoped<ICommandModule, BackupXmlModule>();
    builder.Services.AddScoped<ICommandModule, RestoreModule>();
    builder.Services.AddScoped<ICommandModule, RestoreXmlModule>();
    builder.Services.AddScoped<ICommandModule, MigrateModule>();
    builder.Services.AddScoped<ICommandModule, SeitenRendernModule>();
    builder.Services.AddScoped<ICommandModule, KontoAnlegenModule>();
    builder.Services.AddScoped<ICommandModule, KontoAdminModule>();
    builder.Services.AddScoped<ICommandModule, KontoPasswortModule>();
    builder.Services.AddScoped<ICommandModule, KontoSperrenModule>();

    using var host = builder.Build();
    using var scope = host.Services.CreateScope();
    var modules = scope.ServiceProvider.GetServices<ICommandModule>().ToList();

    var module = args.Length == 0 ? null : modules.FirstOrDefault(m => m.Name == args[0]);
    if (module is null)
    {
        var isHelp = args.Length == 0 || args[0] is "help" or "--help" or "-h";
        var output = isHelp ? Console.Out : Console.Error;
        if (!isHelp)
        {
            output.WriteLine($"Unbekannter Befehl »{args[0]}«.");
            output.WriteLine();
        }
        output.WriteLine("Aufruf: dotnet run --project src/Wissen.Cli -- <Befehl> [Argumente]");
        output.WriteLine();
        output.WriteLine("Befehle:");
        foreach (var m in modules)
        {
            output.WriteLine($"  {m.Usage,-24} {m.Description}");
        }
        return isHelp ? 0 : 2;
    }

    return await module.RunAsync(args[1..], cancellation.Token);
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Abgebrochen.");
    return 130;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Fehler: {ex.Message}");
    return 1;
}
