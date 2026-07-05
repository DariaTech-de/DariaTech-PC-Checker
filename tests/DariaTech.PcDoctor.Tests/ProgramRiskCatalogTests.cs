using DariaTech.PcDoctor.Core;
using Xunit;

namespace DariaTech.PcDoctor.Tests;

public class ProgramRiskCatalogTests
{
    [Theory]
    [InlineData("Adobe Flash Player 32 ActiveX")]
    [InlineData("Adobe Shockwave Player 12.3")]
    [InlineData("Microsoft Silverlight")]
    [InlineData("QuickTime 7")]
    [InlineData("Ask Toolbar")]
    [InlineData("MyWebSearch")]
    [InlineData("WebCompanion")]
    public void Evaluate_KnownRisk_ReturnsRisk(string name)
    {
        var risk = ProgramRiskCatalog.Evaluate(name);
        Assert.NotNull(risk);
        Assert.False(string.IsNullOrWhiteSpace(risk!.Reason));
        Assert.False(string.IsNullOrWhiteSpace(risk.Tip));
    }

    [Theory]
    [InlineData("Java(TM) 6 Update 45")]
    [InlineData("Java 7 Update 80")]
    public void Evaluate_OldJava_IsFlagged(string name)
        => Assert.NotNull(ProgramRiskCatalog.Evaluate(name));

    [Theory]
    [InlineData("Google Chrome")]
    [InlineData("Microsoft Edge")]
    [InlineData("7-Zip 23.01")]
    [InlineData("Microsoft 365 - de-de")]
    [InlineData("Java 8 Update 401")]        // aktuell noch verbreitet -> kein Fehlalarm
    [InlineData("Mozilla Firefox")]
    public void Evaluate_NormalSoftware_ReturnsNull(string name)
        => Assert.Null(ProgramRiskCatalog.Evaluate(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Evaluate_EmptyName_ReturnsNull(string? name)
        => Assert.Null(ProgramRiskCatalog.Evaluate(name));
}
