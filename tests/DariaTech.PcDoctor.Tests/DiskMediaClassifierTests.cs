using DariaTech.PcDoctor.Core;
using Xunit;

namespace DariaTech.PcDoctor.Tests;

public class DiskMediaClassifierTests
{
    [Theory]
    [InlineData(4, null, DiskMediaClassifier.Media.Ssd)]   // MediaType 4 = SSD
    [InlineData(3, null, DiskMediaClassifier.Media.Hdd)]   // MediaType 3 = HDD
    [InlineData(5, null, DiskMediaClassifier.Media.Ssd)]   // SCM -> wie SSD
    public void Classify_ByMediaType(int mediaType, uint? spindle, DiskMediaClassifier.Media expected)
        => Assert.Equal(expected, DiskMediaClassifier.Classify(mediaType, spindle));

    [Theory]
    [InlineData(0, 0u, DiskMediaClassifier.Media.Ssd)]     // unspezifisch + 0 U/min -> SSD
    [InlineData(0, 7200u, DiskMediaClassifier.Media.Hdd)]  // unspezifisch + dreht -> HDD
    public void Classify_FallbackToSpindleSpeed(int mediaType, uint spindle, DiskMediaClassifier.Media expected)
        => Assert.Equal(expected, DiskMediaClassifier.Classify(mediaType, spindle));

    [Fact]
    public void Classify_UnknownWhenNoInfo()
        => Assert.Equal(DiskMediaClassifier.Media.Unknown, DiskMediaClassifier.Classify(0, null));

    [Fact]
    public void Label_IsGerman()
    {
        Assert.Equal("SSD", DiskMediaClassifier.Label(DiskMediaClassifier.Media.Ssd));
        Assert.Contains("Festplatte", DiskMediaClassifier.Label(DiskMediaClassifier.Media.Hdd));
    }
}
