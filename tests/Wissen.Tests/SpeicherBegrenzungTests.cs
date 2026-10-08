using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Wissen.Web;

namespace Wissen.Tests;

public class SpeicherBegrenzungTests
{
    private static HttpContext Request(string method, string path, string? konto = "konto-1", string ip = "203.0.113.7")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        if (konto is not null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, konto)], "Test"));
        }
        return context;
    }

    [Theory]
    [InlineData("/seite/neu")]
    [InlineData("/Seite/bearbeiten/3")]
    [InlineData("/seite/3/version/2/zuruecksetzen")]
    [InlineData("/seite/loeschen/3")]
    public void Partition_CountsChangesPerAccount(string path)
    {
        Assert.Equal("konto:konto-1", SpeicherBegrenzung.Partition(Request("POST", path)));
        Assert.Equal("konto:konto-1", SpeicherBegrenzung.Partition(Request("POST", path, ip: "198.51.100.9")));
        Assert.NotEqual(SpeicherBegrenzung.Partition(Request("POST", path)), SpeicherBegrenzung.Partition(Request("POST", path, konto: "konto-2")));
    }

    [Fact]
    public void Partition_CountsPerAddressWithoutAccount()
    {
        Assert.Equal("absender:203.0.113.7", SpeicherBegrenzung.Partition(Request("POST", "/seite/neu", konto: null)));
    }

    [Theory]
    [InlineData("GET", "/seite/bearbeiten/3")]
    [InlineData("POST", "/seiten")]
    [InlineData("POST", "/Identity/Account/Login")]
    [InlineData("GET", "/doc")]
    public void Partition_IgnoresEverythingElse(string method, string path)
    {
        Assert.Null(SpeicherBegrenzung.Partition(Request(method, path)));
    }

    [Fact]
    public async Task Limiters_AllowOneChangeAtATimeAndTwentyPerPeriod()
    {
        var limiters = SpeicherBegrenzung.CreateLimiters();
        var gleichzeitig = limiters[0];
        var zeitraum = limiters[1];
        var request = Request("POST", "/seite/neu");

        using (var erste = await gleichzeitig.AcquireAsync(request))
        {
            Assert.True(erste.IsAcquired);
            using var zweite = await gleichzeitig.AcquireAsync(request);
            Assert.False(zweite.IsAcquired);
            using var anderesKonto = await gleichzeitig.AcquireAsync(Request("POST", "/seite/neu", konto: "konto-2"));
            Assert.True(anderesKonto.IsAcquired);
        }
        using (var danach = await gleichzeitig.AcquireAsync(request))
        {
            Assert.True(danach.IsAcquired);
        }

        for (var i = 0; i < SpeicherBegrenzung.Anfragen; i++)
        {
            Assert.True((await zeitraum.AcquireAsync(request)).IsAcquired);
        }
        Assert.False((await zeitraum.AcquireAsync(request)).IsAcquired);
        Assert.True((await zeitraum.AcquireAsync(Request("GET", "/seite/neu"))).IsAcquired);
    }
}
