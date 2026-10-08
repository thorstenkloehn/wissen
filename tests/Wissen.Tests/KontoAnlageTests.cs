using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wissen.Infrastructure.Data;
using Wissen.Infrastructure.Identity;

namespace Wissen.Tests;

public class KontoAnlageTests
{
    // Dieselbe Verdrahtung wie in der Konsolenanwendung, nur mit einer Datenbank im Arbeitsspeicher.
    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        var name = Guid.NewGuid().ToString();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(name));
        services.AddIdentityCore<IdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddErrorDescriber<GermanIdentityErrorDescriber>();
        services.AddScoped<KontoAnlage>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Anlegen_CreatesConfirmedAccountThatCanSignIn()
    {
        using var services = CreateServices();
        var konten = services.GetRequiredService<KontoAnlage>();

        var result = await konten.AnlegenAsync(" anna@example.org ", "Geheim-123");

        Assert.True(result.Succeeded);
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByNameAsync("anna@example.org");
        Assert.NotNull(user);
        Assert.Equal("anna@example.org", user.Email);
        Assert.True(user.EmailConfirmed);
        Assert.True(await userManager.CheckPasswordAsync(user, "Geheim-123"));
    }

    [Theory]
    [InlineData("anna@example.org", "kurz", "Passwörter müssen")]
    [InlineData("keine-adresse", "Geheim-123", "ungültig")]
    public async Task Anlegen_RejectsWeakPasswordAndInvalidEmail(string email, string passwort, string meldung)
    {
        using var services = CreateServices();
        var konten = services.GetRequiredService<KontoAnlage>();

        var result = await konten.AnlegenAsync(email, passwort);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Description.Contains(meldung));
        Assert.Empty(services.GetRequiredService<ApplicationDbContext>().Users);
    }

    [Fact]
    public async Task Anlegen_GivesAdministratorRoleOnlyOnRequest()
    {
        using var services = CreateServices();
        var konten = services.GetRequiredService<KontoAnlage>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        Assert.True((await konten.AnlegenAsync("anna@example.org", "Geheim-123")).Succeeded);
        Assert.True((await konten.AnlegenAsync("chef@example.org", "Geheim-123", administrator: true)).Succeeded);

        Assert.False(await userManager.IsInRoleAsync((await userManager.FindByEmailAsync("anna@example.org"))!, Rollen.Administrator));
        Assert.True(await userManager.IsInRoleAsync((await userManager.FindByEmailAsync("chef@example.org"))!, Rollen.Administrator));
    }

    [Fact]
    public async Task AdministratorSetzen_GrantsAndRevokesRole()
    {
        using var services = CreateServices();
        var konten = services.GetRequiredService<KontoAnlage>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        await konten.AnlegenAsync("anna@example.org", "Geheim-123");

        Assert.Null(await konten.AdministratorSetzenAsync("niemand@example.org", true));

        Assert.True((await konten.AdministratorSetzenAsync(" anna@example.org ", true))!.Succeeded);
        Assert.True((await konten.AdministratorSetzenAsync("anna@example.org", true))!.Succeeded);
        var user = (await userManager.FindByEmailAsync("anna@example.org"))!;
        Assert.True(await userManager.IsInRoleAsync(user, Rollen.Administrator));

        Assert.True((await konten.AdministratorSetzenAsync("anna@example.org", false))!.Succeeded);
        Assert.False(await userManager.IsInRoleAsync(user, Rollen.Administrator));
    }

    [Fact]
    public async Task Anlegen_RejectsExistingEmail()
    {
        using var services = CreateServices();
        var konten = services.GetRequiredService<KontoAnlage>();
        await konten.AnlegenAsync("anna@example.org", "Geheim-123");

        var result = await konten.AnlegenAsync("ANNA@example.org", "Geheim-456");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == nameof(IdentityErrorDescriber.DuplicateEmail));
        Assert.Single(services.GetRequiredService<ApplicationDbContext>().Users);
    }
}
