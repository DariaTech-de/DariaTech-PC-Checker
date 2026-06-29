using DariaTech.PcDoctor.Core;
using Xunit;

namespace DariaTech.PcDoctor.Tests;

public class DiagnosticEngineTests
{
    private static CheckResult R(Severity s) => new("Bereich", "Label", "Wert", s);

    [Fact]
    public void Overall_AllOk_ReturnsOk()
    {
        var results = new[] { R(Severity.Ok), R(Severity.Info), R(Severity.Ok) };
        Assert.Equal(Severity.Ok, DiagnosticEngine.Overall(results));
    }

    [Fact]
    public void Overall_WithWarning_ReturnsWarning()
    {
        var results = new[] { R(Severity.Ok), R(Severity.Warning) };
        Assert.Equal(Severity.Warning, DiagnosticEngine.Overall(results));
    }

    [Fact]
    public void Overall_CriticalDominatesWarning()
    {
        var results = new[] { R(Severity.Warning), R(Severity.Critical), R(Severity.Ok) };
        Assert.Equal(Severity.Critical, DiagnosticEngine.Overall(results));
    }

    [Fact]
    public async Task RunAll_OneCheckThrows_DoesNotAbortScan()
    {
        var engine = new DiagnosticEngine(new ICheck[]
        {
            new ThrowingCheck(),
            new OkCheck()
        });

        var results = await engine.RunAllAsync();

        // Der fehlerhafte Check liefert einen Info-Hinweis, der zweite sein OK-Ergebnis.
        Assert.Contains(results, r => r.Area == "Kaputt" && r.Severity == Severity.Info);
        Assert.Contains(results, r => r.Area == "Heil" && r.Severity == Severity.Ok);
    }

    private sealed class ThrowingCheck : ICheck
    {
        public string Area => "Kaputt";
        public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
            => throw new InvalidOperationException("absichtlich");
    }

    private sealed class OkCheck : ICheck
    {
        public string Area => "Heil";
        public Task<IReadOnlyList<CheckResult>> RunAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CheckResult>>(
                new[] { new CheckResult("Heil", "Status", "alles gut", Severity.Ok) });
    }
}
