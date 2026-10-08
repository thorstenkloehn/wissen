using System.ComponentModel.DataAnnotations;
using Wissen.Infrastructure.Models;
using Wissen.Web.Models;

namespace Wissen.Tests;

public class SeiteViewModelTests
{
    private static bool IsValid(object model) =>
        Validator.TryValidateObject(model, new ValidationContext(model), null, validateAllProperties: true);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void MarkdownInhalt_IsLimited(int ueberGrenze, bool gueltig)
    {
        var markdown = new string('x', Seite.MaxMarkdownLength + ueberGrenze);

        Assert.Equal(gueltig, IsValid(new SeiteNeuViewModel { Path = "doc/x", Kategorie = "k", MarkdownInhalt = markdown }));
        Assert.Equal(gueltig, IsValid(new SeiteBearbeitenViewModel { Kategorie = "k", MarkdownInhalt = markdown }));
    }
}
