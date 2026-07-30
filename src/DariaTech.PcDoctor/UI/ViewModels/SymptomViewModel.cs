using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Core.Symptoms;
using DariaTech.PcDoctor.UI.Services;
using Microsoft.Extensions.Logging;

namespace DariaTech.PcDoctor.UI.ViewModels;

/// <summary>
/// Symptom-Assistent: Der Techniker wählt aus, was der Kunde meldet
/// („Windows-Suche geht nicht", „PC ist langsam" …). Der Assistent führt dann
/// gezielt nur die dafür relevanten Prüfungen aus und bietet die passenden
/// Reparaturen in sinnvoller Reihenfolge an – deutlich schneller als ein
/// Komplettscan mit anschließender Suche in allen Kacheln.
/// </summary>
public sealed partial class SymptomViewModel : ObservableObject
{
    private readonly IEnumerable<ICheck> _checks;
    private readonly IReadOnlyList<IFixAction> _allFixes;
    private readonly RepairService _repairService;
    private readonly IDialogService _dialogs;
    private readonly ILogger<SymptomViewModel> _log;

    private CancellationTokenSource? _cts;

    public SymptomViewModel(
        IEnumerable<ICheck> checks,
        IEnumerable<IFixAction> fixes,
        RepairService repairService,
        IDialogService dialogs,
        ILogger<SymptomViewModel> log)
    {
        _checks = checks;
        _allFixes = fixes.ToList();
        _repairService = repairService;
        _dialogs = dialogs;
        _log = log;
    }

    /// <summary>Auswahlliste der Symptome.</summary>
    public IReadOnlyList<Symptom> Symptoms => SymptomCatalog.All;

    /// <summary>Befunde der für das Symptom relevanten Prüfungen (auffällige zuerst).</summary>
    public ObservableCollection<CheckResult> Findings { get; } = new();

    /// <summary>Empfohlene Reparaturen zum gewählten Symptom, in Reihenfolge.</summary>
    public ObservableCollection<IFixAction> RecommendedFixes { get; } = new();

    /// <summary>Live-Log der laufenden Reparatur.</summary>
    public ObservableCollection<string> FixLog { get; } = new();

    [ObservableProperty] private Symptom? _selectedSymptom;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = "Wählen Sie aus, was der Kunde meldet – " +
        "der Assistent prüft dann nur die dafür relevanten Bereiche.";
    [ObservableProperty] private bool _hasFindings;
    [ObservableProperty] private string _advice = string.Empty;
    [ObservableProperty] private Severity _worstSeverity = Severity.Ok;
    [ObservableProperty] private string _currentFixTitle = string.Empty;
    [ObservableProperty] private bool _isFixRunning;

    /// <summary>Startet die gezielte Prüfung für das gewählte Symptom.</summary>
    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync()
    {
        if (SelectedSymptom is not Symptom symptom) return;

        IsBusy = true;
        Findings.Clear();
        RecommendedFixes.Clear();
        HasFindings = false;
        Advice = symptom.Advice;
        _cts = new CancellationTokenSource();

        var results = new List<CheckResult>();
        try
        {
            // Nur die Prüfungen ausführen, die zum Symptom gehören.
            var relevant = _checks
                .Where(c => symptom.CheckAreas.Contains(c.Area, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (relevant.Count == 0)
                _log.LogWarning("Symptom {Id}: keine passenden Prüfungen gefunden", symptom.Id);

            foreach (var check in relevant)
            {
                _cts.Token.ThrowIfCancellationRequested();
                Status = $"Prüfe: {check.Area} …";
                try
                {
                    results.AddRange(await check.RunAsync(_cts.Token).ConfigureAwait(true));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Prüfung im Assistenten fehlgeschlagen: {Area}", check.Area);
                    results.Add(new CheckResult(check.Area, "Hinweis", "nicht prüfbar", Severity.Info));
                }
            }

            ShowFindings(results);
            LoadFixes(symptom);

            var problems = results.Count(r => r.Severity >= Severity.Warning);
            Status = problems > 0
                ? $"{problems} auffällige(r) Befund(e) – empfohlene Reparaturen unten."
                : "Keine Auffälligkeiten in den geprüften Bereichen. Empfohlene Schritte stehen trotzdem bereit.";
        }
        catch (OperationCanceledException)
        {
            Status = "Prüfung abgebrochen.";
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Symptom-Prüfung fehlgeschlagen");
            Status = $"Prüfung fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private bool CanAnalyze() => SelectedSymptom is not null && !IsBusy && !IsFixRunning;

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    /// <summary>Auffällige Befunde zuerst – der Techniker sieht das Wichtigste oben.</summary>
    private void ShowFindings(IReadOnlyList<CheckResult> results)
    {
        foreach (var r in results.OrderByDescending(r => r.Severity))
            Findings.Add(r);

        HasFindings = Findings.Count > 0;
        WorstSeverity = DiagnosticEngine.Overall(results);
    }

    /// <summary>Übernimmt die empfohlenen Reparaturen in der Katalog-Reihenfolge.</summary>
    private void LoadFixes(Symptom symptom)
    {
        foreach (var type in symptom.FixTypes)
        {
            var fix = _allFixes.FirstOrDefault(f => f.GetType() == type);
            if (fix is not null) RecommendedFixes.Add(fix);
            else _log.LogWarning("Symptom {Id}: Reparatur {Type} nicht registriert", symptom.Id, type.Name);
        }
    }

    /// <summary>
    /// Rückfrage, wenn kein Wiederherstellungspunkt angelegt werden konnte –
    /// gleiche Regel wie im Dashboard: ohne ausdrückliche Freigabe wird nichts
    /// verändert.
    /// </summary>
    private bool AskProceedWithoutRestorePoint(string reason)
        => _dialogs.Confirm("Kein Wiederherstellungspunkt möglich",
            $"{reason}\n\n" +
            "Damit gibt es KEINE Rückfallebene, falls die Reparatur Probleme macht.\n\n" +
            "Empfehlung: abbrechen und zuerst „Systemwiederherstellung einschalten“ ausführen.\n\n" +
            "Trotzdem ohne Wiederherstellungspunkt fortfahren?");

    /// <summary>Führt eine empfohlene Reparatur aus (mit Bestätigung, wie im Dashboard).</summary>
    [RelayCommand(CanExecute = nameof(CanRunFix))]
    private async Task RunFixAsync(IFixAction? fix)
    {
        if (fix is null) return;

        var note = fix.RequiresRestorePoint
            ? "\n\nVorher wird automatisch ein Systemwiederherstellungspunkt angelegt."
            : string.Empty;
        if (!_dialogs.Confirm(fix.Title, $"{fix.Description}{note}\n\nFortfahren?"))
            return;

        IsFixRunning = true;
        CurrentFixTitle = fix.Title;
        FixLog.Clear();
        _cts = new CancellationTokenSource();
        var progress = new Progress<string>(s => FixLog.Add(s));

        FixOutcome outcome;
        try
        {
            outcome = await _repairService
                .RunAsync(fix, progress, _cts.Token, AskProceedWithoutRestorePoint)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Reparatur im Assistenten fehlgeschlagen: {Title}", fix.Title);
            outcome = new FixOutcome(false, ex.Message);
        }
        finally
        {
            IsFixRunning = false;
            _cts?.Dispose();
            _cts = null;
        }

        _dialogs.Inform(fix.Title, outcome.Message);

        // Nach der Reparatur erneut prüfen, damit die Wirkung sichtbar wird.
        if (SelectedSymptom is not null)
            await AnalyzeAsync().ConfigureAwait(true);
    }

    private bool CanRunFix() => !IsBusy && !IsFixRunning;

    partial void OnSelectedSymptomChanged(Symptom? value)
    {
        Advice = value?.Advice ?? string.Empty;
        AnalyzeCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value) => RefreshCommands();
    partial void OnIsFixRunningChanged(bool value) => RefreshCommands();

    private void RefreshCommands()
    {
        AnalyzeCommand.NotifyCanExecuteChanged();
        RunFixCommand.NotifyCanExecuteChanged();
    }
}
