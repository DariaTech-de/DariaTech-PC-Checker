using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.UI.ViewModels;

/// <summary>
/// Eine Dashboard-Kachel: alle Prüfergebnisse eines Bereichs plus die für den
/// Bereich verfügbaren Reparaturen. Die Kachelfarbe ergibt sich aus dem
/// schlechtesten Ergebnis (<see cref="Severity"/>).
/// </summary>
public sealed partial class AreaResultViewModel : ObservableObject
{
    public AreaResultViewModel(string area, IEnumerable<CheckResult> results, IReadOnlyList<IFixAction> fixes)
    {
        Area = area;
        Fixes = fixes;
        Results = new ObservableCollection<CheckResult>(results);
        Severity = DiagnosticEngine.Overall(Results);
    }

    public string Area { get; }

    public ObservableCollection<CheckResult> Results { get; }

    /// <summary>Für diesen Bereich anwendbare Reparaturen (kann leer sein).</summary>
    public IReadOnlyList<IFixAction> Fixes { get; }

    public bool HasFixes => Fixes.Count > 0;

    [ObservableProperty]
    private Severity _severity;

    /// <summary>Kurzbefund für die Kachel (erstes auffälliges bzw. erstes Ergebnis).</summary>
    public string Summary
    {
        get
        {
            var worst = Results.FirstOrDefault(r => r.Severity == Severity)
                ?? Results.FirstOrDefault();
            return worst is null ? "—" : $"{worst.Label}: {worst.Value}";
        }
    }

    /// <summary>Ersetzt die Ergebnisse nach einem Re-Check und aktualisiert die Ampel.</summary>
    public void Update(IEnumerable<CheckResult> results)
    {
        Results.Clear();
        foreach (var r in results) Results.Add(r);
        Severity = DiagnosticEngine.Overall(Results);
        OnPropertyChanged(nameof(Summary));
    }
}
