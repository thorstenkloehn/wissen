using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Wissen.Web.Areas.Identity;

namespace Wissen.Tests;

public class PasswortBegrenzungTests
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
    [InlineData("/Identity/Account/Manage/ChangePassword")]
    [InlineData("/identity/account/manage/changepassword/")]
    [InlineData("/Identity/Account/Manage/DeletePersonalData")]
    public void Partition_CountsPasswordChecksPerAccount(string path)
    {
        Assert.Equal("konto:konto-1", PasswortBegrenzung.Partition(Request("POST", path)));
        Assert.Equal("konto:konto-1", PasswortBegrenzung.Partition(Request("POST", path, ip: "198.51.100.9")));
        Assert.NotEqual(PasswortBegrenzung.Partition(Request("POST", path)), PasswortBegrenzung.Partition(Request("POST", path, konto: "konto-2")));
    }

    [Fact]
    public void Partition_CountsPerAddressWithoutAccount()
    {
        Assert.Equal("absender:203.0.113.7", PasswortBegrenzung.Partition(Request("POST", "/Identity/Account/Manage/ChangePassword", konto: null)));
    }

    [Theory]
    [InlineData("GET", "/Identity/Account/Manage/ChangePassword")]
    [InlineData("POST", "/Identity/Account/Manage/ChangePasswordX")]
    [InlineData("POST", "/Identity/Account/Manage/Email")]
    [InlineData("POST", "/Identity/Account/Login")]
    [InlineData("POST", "/seite/neu")]
    public void Partition_IgnoresEverythingElse(string method, string path)
    {
        Assert.Null(PasswortBegrenzung.Partition(Request(method, path)));
    }

    [Fact]
    public async Task Limiter_AllowsFiveAttemptsPerAccountAndPeriod()
    {
        var limiter = PasswortBegrenzung.CreateLimiter();
        var request = Request("POST", "/Identity/Account/Manage/ChangePassword");

        for (var i = 0; i < PasswortBegrenzung.Versuche; i++)
        {
            using var lease = await limiter.AcquireAsync(request);
            Assert.True(lease.IsAcquired);
        }

        using var zuViel = await limiter.AcquireAsync(request);
        Assert.False(zuViel.IsAcquired);
        // Die andere Seite zählt für dasselbe Konto mit, ein anderes Konto nicht.
        using var andereSeite = await limiter.AcquireAsync(Request("POST", "/Identity/Account/Manage/DeletePersonalData"));
        Assert.False(andereSeite.IsAcquired);
        using var anderesKonto = await limiter.AcquireAsync(Request("POST", "/Identity/Account/Manage/ChangePassword", konto: "konto-2"));
        Assert.True(anderesKonto.IsAcquired);
    }
}
