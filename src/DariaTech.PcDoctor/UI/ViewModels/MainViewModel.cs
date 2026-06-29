using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Fixes;
using DariaTech.PcDoctor.Models;
using DariaTech.PcDoctor.UI.Services;
using Microsoft.Extensions.Logging;

namespace DariaTech.PcDoctor.UI.ViewModels;

/// <summary>
/// Steuert das Dashboard: startet den Scan, gruppiert die Ergebnisse zu
/// Ampel-Kacheln, blendet das Detailpanel ein, führt Reparaturen aus und
/// exportiert den HTML-Bericht. Hält die UI dünn – die eigentliche Logik liegt
/// in Engine/Checks/Fixes.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly DiagnosticEngine _engine;
    private readonly IEnumerable<ICheck> _checks;
    private readonly RepairService _repairService;
    private readonly ReportExporter _reportExporter;
    private readonly ScanOptions _scanOptions;
    private readonly IDialogService _dialogs;
    private readonly IReadOnlyList<IFixAction> _allFixes;
    private readonly ILogger<MainViewModel> _log;

    private CancellationTokenSource? _cts;
    private List<CheckResult> _lastResults = new();

    public MainViewModel(
        DiagnosticEngine engine,
        IEnumerable<ICheck> checks,
        IEnumerable<IFixAction> fixes,
        RepairService repairService,
        ReportExporter reportExporter,
        ScanOptions scanOptions,
        IDialogService dialogs,
        GamingViewModel gaming,
        ILogger<MainViewModel> log)
    {
        _engine = engine;
        _checks = checks;
        _allFixes = fixes.ToList();
        _repairService = repairService;
        _reportExporter = reportExporter;
        _scanOptions = scanOptions;
        _dialogs = dialogs;
        Gaming = gaming;
        _log = log;
    }

    /// <summary>ViewModel des Tabs „Gaming &amp; Stresstest".</summary>
    public GamingViewModel Gaming { get; }

    public ObservableCollection<AreaResultViewModel> Areas { get; } = new();
    public ObservableCollection<string> FixLog { get; } = new();

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private string _statusText = "Bereit für den Scan.";
    [ObservableProperty] private Severity _overallSeverity = Severity.Ok;
    [ObservableProperty] private int _criticalCount;
    [ObservableProperty] private int _warningCount;
    [ObservableProperty] private int _healthScore = 100;

    // Übergabe-/Kundendaten für den Bericht
    [ObservableProperty] private string _customerName = string.Empty;
    [ObservableProperty] private string _orderNumber = string.Empty;
    [ObservableProperty] private string _technician = string.Empty;

    [ObservableProperty] private AreaResultViewModel? _selectedArea;
    [ObservableProperty] private bool _isFixRunning;
    [ObservableProperty] private string _currentFixTitle = string.Empty;

    /// <summary>Schnellmodus: überspringt die langsame Windows-Update-Suche.</summary>
    public bool SkipWindowsUpdate
    {
        get => _scanOptions.SkipWindowsUpdate;
        set
        {
            if (_scanOptions.SkipWindowsUpdate == value) return;
            _scanOptions.SkipWindowsUpdate = value;
            OnPropertyChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        IsScanning = true;
        HasResults = false;
        SelectedArea = null;
        Areas.Clear();
        _cts = new CancellationTokenSource();
        var progress = new Progress<string>(s => StatusText = s);

        try
        {
            _lastResults = (await _engine.RunAllAsync(progress, _cts.Token)
                .ConfigureAwait(true)).ToList();
            BuildAreas(_lastResults);
            UpdateOverall();
            HasResults = true;
            StatusText = $"Scan abgeschlossen – {Areas.Count} Bereiche geprüft.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan abgebrochen.";
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Scan fehlgeschlagen");
            StatusText = $"Scan fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private bool CanScan() => !IsScanning && !IsFixRunning;

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private void SelectArea(AreaResultViewModel? area) => SelectedArea = area;

    [RelayCommand(CanExecute = nameof(CanExport))]
    private void ExportReport() => ExportInternal(asPdf: false);

    [RelayCommand(CanExecute = nameof(CanExport))]
    private void ExportPdf() => ExportInternal(asPdf: true);

    private void ExportInternal(bool asPdf)
    {
        try
        {
            var context = new ReportContext
            {
                CustomerName = CustomerName,
                OrderNumber = OrderNumber,
                Technician = Technician
            };

            string? path;
            if (asPdf)
            {
                path = _reportExporter.ExportPdf(_lastResults, context);
                if (path is null)
                {
                    // Edge nicht gefunden – HTML als Ersatz erzeugen.
                    path = _reportExporter.Export(_lastResults, context);
                    _dialogs.Inform("PDF nicht möglich",
                        "Microsoft Edge wurde nicht gefunden – es wurde stattdessen die HTML-Datei erstellt:\n" + path);
                    OpenFile(path);
                    return;
                }
            }
            else
            {
                path = _reportExporter.Export(_lastResults, context);
            }

            _dialogs.Inform("Bericht erstellt", $"Der Bericht wurde gespeichert:\n{path}");
            OpenFile(path);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Bericht-Export fehlgeschlagen");
            _dialogs.Inform("Fehler", $"Der Bericht konnte nicht erstellt werden:\n{ex.Message}");
        }
    }

    private static void OpenFile(string path)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
        catch { /* Öffnen optional */ }
    }

    private bool CanExport() => HasResults && !IsScanning && !IsFixRunning;

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
            outcome = await _repairService.RunAsync(fix, progress, _cts.Token).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Reparatur fehlgeschlagen: {Title}", fix.Title);
            outcome = new FixOutcome(false, ex.Message);
        }
        finally
        {
            IsFixRunning = false;
            _cts?.Dispose();
            _cts = null;
        }

        _dialogs.Inform(fix.Title, outcome.Message);

        // Betroffenen Bereich automatisch neu prüfen.
        if (SelectedArea is not null)
            await RecheckAreaAsync(SelectedArea).ConfigureAwait(true);
    }

    private bool CanRunFix() => !IsScanning && !IsFixRunning;

    private async Task RecheckAreaAsync(AreaResultViewModel area)
    {
        var check = _checks.FirstOrDefault(c => c.Area == area.Area);
        if (check is null) return;
        try
        {
            var results = await check.RunAsync().ConfigureAwait(true);
            area.Update(results);
            _lastResults = _lastResults.Where(r => r.Area != area.Area).Concat(results).ToList();
            UpdateOverall();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Re-Check fehlgeschlagen: {Area}", area.Area);
        }
    }

    private void BuildAreas(IEnumerable<CheckResult> results)
    {
        foreach (var group in results.GroupBy(r => r.Area))
            Areas.Add(new AreaResultViewModel(group.Key, group, FixesForArea(group.Key)));
    }

    private void UpdateOverall()
    {
        OverallSeverity = DiagnosticEngine.Overall(_lastResults);
        CriticalCount = _lastResults.Count(r => r.Severity == Severity.Critical);
        WarningCount = _lastResults.Count(r => r.Severity == Severity.Warning);
        HealthScore = ReportExporter.HealthScore(_lastResults);
    }

    /// <summary>Ordnet den registrierten Reparaturen ihre Bereiche zu.</summary>
    private IReadOnlyList<IFixAction> FixesForArea(string area)
    {
        IEnumerable<Type> wanted = area switch
        {
            "Datenträger – Speicherplatz" => new[] { typeof(ClearTempFilesFix), typeof(CheckDiskFix) },
            "Datenträger – Gesundheit (SMART)" => new[] { typeof(CheckDiskFix) },
            "System & Betriebssystem" => new[] { typeof(SystemFileRepairFix), typeof(ClearTempFilesFix) },
            "Netzwerk" => new[] { typeof(FlushDnsFix), typeof(WinsockResetFix) },
            "Windows-Sicherheit" => new[] { typeof(DefenderQuickScanFix) },
            "Windows-Updates" => new[] { typeof(WindowsUpdateRepairFix) },
            "Akku" => new[] { typeof(BatteryReportFix) },
            _ => Array.Empty<Type>()
        };
        return _allFixes.Where(f => wanted.Contains(f.GetType())).ToList();
    }

    // CanExecute-Neuauswertung bei Statuswechsel.
    partial void OnIsScanningChanged(bool value) => RefreshCommands();
    partial void OnIsFixRunningChanged(bool value) => RefreshCommands();
    partial void OnHasResultsChanged(bool value)
    {
        ExportReportCommand.NotifyCanExecuteChanged();
        ExportPdfCommand.NotifyCanExecuteChanged();
    }

    private void RefreshCommands()
    {
        ScanCommand.NotifyCanExecuteChanged();
        ExportReportCommand.NotifyCanExecuteChanged();
        ExportPdfCommand.NotifyCanExecuteChanged();
        RunFixCommand.NotifyCanExecuteChanged();
    }
}
