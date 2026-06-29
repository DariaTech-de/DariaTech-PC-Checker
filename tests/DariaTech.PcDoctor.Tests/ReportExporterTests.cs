using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Models;
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
    public void HealthScore_SubtractsForWarningsAndCriticals()
    {
        var results = new[]
        {
            new CheckResult("A", "x", "y", Severity.Ok),
            new CheckResult("B", "x", "y", Severity.Warning),
            new CheckResult("C", "x", "y", Severity.Critical)
        };
        // 100 - 20 (crit) - 7 (warn) = 73
        Assert.Equal(73, ReportExporter.HealthScore(results));
    }

    [Fact]
    public void HealthScore_ClampedToZero()
    {
        var results = Enumerable.Range(0, 10)
            .Select(_ => new CheckResult("A", "x", "y", Severity.Critical))
            .ToArray();
        Assert.Equal(0, ReportExporter.HealthScore(results));
    }

    [Fact]
    public void BuildHtml_WithContext_ShowsHandoverBlockAndScore()
    {
        var exporter = new ReportExporter();
        var results = new[] { new CheckResult("A", "x", "y", Severity.Ok) };
        var context = new ReportContext
        {
            CustomerName = "Max Mustermann",
            OrderNumber = "2026-0815",
            Technician = "D. Aria"
        };

        var html = exporter.BuildHtml(results, "TESTPC", DateTime.Now, context);

        Assert.Contains("Übergabe", html);
        Assert.Contains("Max Mustermann", html);
        Assert.Contains("2026-0815", html);
        Assert.Contains("Gesundheit 100/100", html);
    }

    [Fact]
    public void BuildHtml_RendersDetailTextPerFinding()
    {
        var exporter = new ReportExporter();
        var results = new[]
        {
            new CheckResult("Treiber & Geräte", "Problem", "Realtek Audio", Severity.Warning,
                "Geräte-Manager-Code 28: Treiber nicht installiert.")
        };

        var html = exporter.BuildHtml(results, "PC", DateTime.Now);

        Assert.Contains("class='detail'", html);
        Assert.Contains("Treiber nicht installiert", html);
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
