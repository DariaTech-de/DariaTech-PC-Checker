using System.Globalization;
using DariaTech.PcDoctor.Core;
using Xunit;

namespace DariaTech.PcDoctor.Tests;

public class ByteSizeTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1.0 MB")]
    [InlineData(1073741824, "1.0 GB")]
    [InlineData(1099511627776, "1.0 TB")]
    public void Human_FormatsWithInvariantCulture(long bytes, string expected)
    {
        // Kultur-unabhängig prüfen (Dezimaltrennzeichen im Test fixiert).
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try { Assert.Equal(expected, ByteSize.Human(bytes)); }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void Human_NegativeClampsToZero() => Assert.Equal("0 B", ByteSize.Human(-5));
}
