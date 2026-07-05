using DariaTech.PcDoctor.Core;
using Xunit;

namespace DariaTech.PcDoctor.Tests;

public class WlanInfoParserTests
{
    private const string GermanOutput = """
        Es ist 1 Schnittstelle im System vorhanden:

            Name                   : WLAN
            Beschreibung           : Intel(R) Wi-Fi 6 AX201
            GUID                   : 1234
            Physische Adresse      : aa:bb:cc:dd:ee:ff
            Status                 : Verbunden
            SSID                   : FritzBox 7590
            BSSID                  : 11:22:33:44:55:66
            Netzwerktyp            : Infrastruktur
            Funktyp                : 802.11ax
            Authentifizierung      : WPA2-Personal
            Signal                 : 82%
            Kanal                  : 36
            Empfangsrate (MBit/s)  : 866
        """;

    private const string EnglishOutput = """
        There is 1 interface on the system:

            Name                   : Wi-Fi
            SSID                   : MyHomeNetwork
            BSSID                  : 11:22:33:44:55:66
            Radio type             : 802.11n
            Signal                 : 47%
            Channel                : 11
        """;

    [Fact]
    public void Parse_German_ExtractsAllFields()
    {
        var info = WlanInfoParser.Parse(GermanOutput);
        Assert.Equal(82, info.SignalPercent);
        Assert.Equal("FritzBox 7590", info.Ssid);   // nicht die BSSID
        Assert.Equal(36, info.Channel);
        Assert.Equal("802.11ax", info.Radio);
    }

    [Fact]
    public void Parse_English_ExtractsAllFields()
    {
        var info = WlanInfoParser.Parse(EnglishOutput);
        Assert.Equal(47, info.SignalPercent);
        Assert.Equal("MyHomeNetwork", info.Ssid);
        Assert.Equal(11, info.Channel);
        Assert.Equal("802.11n", info.Radio);
    }

    [Fact]
    public void Parse_EmptyOrGarbage_ReturnsNulls()
    {
        var info = WlanInfoParser.Parse("kein WLAN");
        Assert.Null(info.SignalPercent);
        Assert.Null(info.Ssid);
        Assert.Null(info.Channel);
        Assert.Null(info.Radio);
    }
}
