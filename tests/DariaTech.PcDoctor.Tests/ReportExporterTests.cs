using DariaTech.PcDoctor.Core;
using Xunit;

namespace DariaTech.PcDoctor.Tests;

public class ReportExporterTests
{
    [Fact]
    public void BuildHtml_HealthySystem_ShowsNoIssuesBanner()
    {
        var exporter = new ReportExporter();
        var results = new[]
        {
            new CheckResult("System & Betriebssystem", "Windows", "Windows 11", Severity.Info),
            new CheckResult("Datenträger – Speicherplatz", "Laufwerk C:", "120 GB frei (40 %)", Severity.Ok)
        };

        var html = exporter.BuildHtml(results, "TESTPC", new DateTime(2026, 6, 28, 10, 0, 0));

        Assert.Contains("Keine Auffälligkeiten", html);
        Assert.Contains("TESTPC", html);
        Assert.Contains("Datenträger", html);
    }

    [Fact]
    public void BuildHtml_WithCritical_ShowsCriticalAmpel()
    {
        var exporter = new ReportExporter();
        var results = new[]
        {
            new CheckResult("Datenträger – Gesundheit (SMART)", "Status: Unhealthy", "SSD",
                Severity.Critical, "Datenträger defekt – sofort Backup!")
        };

        var html = exporter.BuildHtml(results, "TESTPC", DateTime.Now);

        Assert.Contains("ampel crit", html);
        Assert.Contains("sofort Backup", html);
    }

    [Fact]
    public void BuildHtml_EncodesHtmlSpecialCharacters()
    {
        var exporter = new ReportExporter();
        var results = new[]
        {
            new CheckResult("Test", "Gerät <x>", "A & B", Severity.Info)
        };

        var html = exporter.BuildHtml(results, "PC", DateTime.Now);

        Assert.Contains("Gerät &lt;x&gt;", html);
        Assert.Contains("A &amp; B", html);
    }
}
