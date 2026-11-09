namespace ServiceLib.Tests.Handler;

public class IpPurityParseTests
{
    [Test]
    public async Task ResidentialIp_ShouldReportPurityAndResidentialType()
    {
        var json = """
                   {"ip":"104.28.1.1","asn":13335,"isResidential":true,"isBroadcast":false,"fraudScore":3}
                   """;

        var result = ConnectionHandler.ParseIpPurity(json);

        await result.HasValue.Should().BeTrue();
        await result.Value.Purity.Should().BeEqualTo(97);
        await result.Value.Type.Should().BeEqualTo(ResUI.IpPurityResidential);
        await result.Value.Ip.Should().BeEqualTo("104.28.1.1");
    }

    [Test]
    public async Task HostingIp_ShouldReportHostingType()
    {
        var json = """
                   {"ip":"198.51.100.7","isResidential":false,"isBroadcast":false,"fraudScore":55}
                   """;

        var result = ConnectionHandler.ParseIpPurity(json);

        await result.Value.Purity.Should().BeEqualTo(45);
        await result.Value.Type.Should().BeEqualTo(ResUI.IpPurityHosting);
    }

    [Test]
    public async Task BroadcastIp_ShouldWinOverResidentialFlag()
    {
        var json = """
                   {"ip":"203.0.113.9","isResidential":true,"isBroadcast":true,"fraudScore":90}
                   """;

        var result = ConnectionHandler.ParseIpPurity(json);

        await result.Value.Purity.Should().BeEqualTo(10);
        await result.Value.Type.Should().BeEqualTo(ResUI.IpPurityBroadcast);
    }

    [Test]
    [Arguments(0, 100)]
    [Arguments(100, 0)]
    [Arguments(120, 0)]
    [Arguments(-5, 100)]
    public async Task FraudScore_ShouldBeClampedIntoPurityRange(int fraudScore, int expected)
    {
        var json = $$"""{"ip":"203.0.113.10","fraudScore":{{fraudScore}}}""";

        var result = ConnectionHandler.ParseIpPurity(json);

        await result.Value.Purity.Should().BeEqualTo(expected);
    }

    [Test]
    public async Task LiveIppurePayloadShape_ShouldParse()
    {
        // Captured from https://my.ippure.com/v1/info to keep the field names honest.
        var json = """
                   {
                       "ip": "124.65.231.2",
                       "asn": 4808,
                       "asOrganization": "China Unicom Beijing province network",
                       "country": "China",
                       "countryCode": "CN",
                       "region": "Beijing",
                       "regionCode": "BJ",
                       "city": "Beijing",
                       "timezone": "Asia/Shanghai",
                       "longitude": "116.39723",
                       "latitude": "39.9075",
                       "postalCode": "100000",
                       "fraudScore": 3,
                       "isResidential": true,
                       "isBroadcast": false,
                       "userAgent": "curl/8.7.1"
                   }
                   """;

        var result = ConnectionHandler.ParseIpPurity(json);

        await result.HasValue.Should().BeTrue();
        await result.Value.Purity.Should().BeEqualTo(97);
        await result.Value.Type.Should().BeEqualTo(ResUI.IpPurityResidential);
        await result.Value.Ip.Should().BeEqualTo("124.65.231.2");
        await result.Value.ToString().Should().BeEqualTo($"97% {ResUI.IpPurityResidential}");
    }

    [Test]
    public async Task MissingFraudScore_ShouldReturnNull()
    {
        var result = ConnectionHandler.ParseIpPurity("""{"ip":"203.0.113.11"}""");
        await ((object?)result).Should().BeNull();
    }

    [Test]
    [Arguments("")]
    [Arguments("not a json")]
    public async Task InvalidPayload_ShouldReturnNull(string json)
    {
        var result = ConnectionHandler.ParseIpPurity(json);
        await ((object?)result).Should().BeNull();
    }

    [Test]
    public async Task IpApiFallback_HostingExitShouldBeLowestPurity()
    {
        var json = """{"status":"success","countryCode":"SG","city":"Singapore","mobile":false,"proxy":true,"hosting":true,"query":"56.10.152.4"}""";

        var result = ConnectionHandler.ParseIpApiRisk(json);

        await result.HasValue.Should().BeTrue();
        await result.Value.Purity.Should().BeEqualTo(25);
        await result.Value.Type.Should().BeEqualTo(ResUI.IpPurityHosting);
        await result.Value.Ip.Should().BeEqualTo("56.10.152.4");
    }

    [Test]
    public async Task IpApiFallback_ProxyWithoutHostingShouldReportProxyExit()
    {
        var json = """{"status":"success","mobile":false,"proxy":true,"hosting":false,"query":"192.0.2.20"}""";

        var result = ConnectionHandler.ParseIpApiRisk(json);

        await result.Value.Purity.Should().BeEqualTo(60);
        await result.Value.Type.Should().BeEqualTo(ResUI.IpPurityProxy);
    }

    [Test]
    public async Task IpApiFallback_CleanExitShouldReportResidential()
    {
        var json = """{"status":"success","mobile":false,"proxy":false,"hosting":false,"query":"192.0.2.21"}""";

        var result = ConnectionHandler.ParseIpApiRisk(json);

        await result.Value.Purity.Should().BeEqualTo(90);
        await result.Value.Type.Should().BeEqualTo(ResUI.IpPurityResidential);
    }

    [Test]
    public async Task IpApiFallback_FailedStatusShouldReturnNull()
    {
        var result = ConnectionHandler.ParseIpApiRisk("""{"status":"fail","message":"reserved range"}""");
        await ((object?)result).Should().BeNull();
    }
}
