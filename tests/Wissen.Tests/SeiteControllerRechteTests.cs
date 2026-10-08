using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Wissen.Infrastructure.Identity;
using Wissen.Web.Controllers;

namespace Wissen.Tests;

public class SeiteControllerRechteTests
{
    [Theory]
    [InlineData(nameof(SeiteController.Loeschen), Rollen.Administrator)]
    [InlineData(nameof(SeiteController.LoeschenBestaetigt), Rollen.Administrator)]
    [InlineData(nameof(SeiteController.Bearbeiten), null)]
    [InlineData(nameof(SeiteController.Zuruecksetzen), null)]
    public void OnlyDeletingRequiresAdministrator(string action, string? rolle)
    {
        var methods = typeof(SeiteController).GetMethods().Where(m => m.Name == action).ToList();

        Assert.NotEmpty(methods);
        Assert.All(methods, m => Assert.Equal(rolle, m.GetCustomAttribute<AuthorizeAttribute>()?.Roles));
        Assert.NotNull(typeof(SeiteController).GetCustomAttribute<AuthorizeAttribute>());
    }
}
