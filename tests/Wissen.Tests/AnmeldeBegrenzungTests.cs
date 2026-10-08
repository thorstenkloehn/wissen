using System.Net;
using Microsoft.AspNetCore.Http;
using Wissen.Web.Areas.Identity;

namespace Wissen.Tests;

public class AnmeldeBegrenzungTests
{
    private static HttpContext Request(string method, string path, string ip = "203.0.113.7")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return context;
    }

    [Theory]
    [InlineData("POST", "/Identity/Account/Login")]
    [InlineData("POST", "/identity/account/login")]
    [InlineData("POST", "/Identity/Account/LoginWith2fa")]
    [InlineData("POST", "/Identity/Account/LoginWithRecoveryCode")]
    public void Partition_CountsLoginAttemptsPerAddress(string method, string path)
    {
        Assert.Equal("203.0.113.7", AnmeldeBegrenzung.Partition(Request(method, path)));
    }

    [Theory]
    [InlineData("GET", "/Identity/Account/Login")]
    [InlineData("POST", "/Identity/Account/Logout")]
    [InlineData("POST", "/seite/neu")]
    [InlineData("GET", "/doc")]
    public void Partition_IgnoresEverythingElse(string method, string path)
    {
        Assert.Null(AnmeldeBegrenzung.Partition(Request(method, path)));
    }

    [Fact]
    public void Absender_GroupsIpv6ByNetworkAndUnwrapsMappedIpv4()
    {
        Assert.Equal(
            AnmeldeBegrenzung.Absender(IPAddress.Parse("2001:db8:1:2::1")),
            AnmeldeBegrenzung.Absender(IPAddress.Parse("2001:db8:1:2:ffff:ffff:ffff:ffff")));
        Assert.NotEqual(
            AnmeldeBegrenzung.Absender(IPAddress.Parse("2001:db8:1:2::1")),
            AnmeldeBegrenzung.Absender(IPAddress.Parse("2001:db8:1:3::1")));
        Assert.Equal("203.0.113.7", AnmeldeBegrenzung.Absender(IPAddress.Parse("::ffff:203.0.113.7")));
        Assert.Equal("unbekannt", AnmeldeBegrenzung.Absender(null));
    }
}
