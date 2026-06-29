using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Core.StressTest;
using DariaTech.PcDoctor.Models;
using DariaTech.PcDoctor.UI.Services;
using Microsoft.Extensions.Logging;

namespace DariaTech.PcDoctor.UI.ViewModels;

/// <summary>
/// „Gaming &amp; Stresstest": Live-Monitoring der Hardwaresensoren (Tachos +
/// Verlaufsdiagramm) und ein abbrechbarer Stresstest mit Abschlussbericht.
/// </summary>
public sealed partial class GamingViewModel : ObservableObject
{
    private const int HistoryLength = 90;

    private readonly ISensorService _sensors;
    private readonly StressTestService _stress;
    private readonly IDialogService _dialogs;
    private readonly ILogger<GamingViewModel> _log;
    private readonly DispatcherTimer _timer;
    private CancellationTokenSource? _stressCts;

    public GamingViewModel(ISensorService sensors, StressTestService stress,
        IDialogService dialogs, ILogger<GamingViewModel> log)
    {
        _sensors = sensors;
        _stress = stress;
        _dialogs = dialogs;
        _log = log;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
    }

    public ObservableCollection<double> CpuTempHistory { get; } = new();
    public ObservableCollection<double> GpuTempHistory { get; } = new();

    [ObservableProperty] private bool _sensorAvailable;
    [ObservableProperty] private string _sensorStatus = "Live-Überwachung starten, um Temperaturen und Auslastung zu sehen.";
    [ObservableProperty] private bool _isMonitoring;

    [ObservableProperty] private double _cpuTempC;
    [ObservableProperty] private bool _hasCpuTemp;
    [ObservableProperty] private double _gpuTempC;
    [ObservableProperty] private bool _hasGpuTemp;
    [ObservableProperty] private double _cpuLoadPct;
    [ObservableProperty] private bool _hasCpuLoad;
    [ObservableProperty] private double _maxFanRpm;
    [ObservableProperty] private bool _hasFan;

    [ObservableProperty] private double _stressDurationMinutes = 3;
    [ObservableProperty] private bool _isStressRunning;
    [ObservableProperty] private string _stressStatus = string.Empty;
    [ObservableProperty] private double _stressProgressPercent;
    [ObservableProperty] private StressTestReport? _stressReport;

    public bool HasReport => StressReport is not null;
    public string MonitoringButtonText => IsMonitoring ? "Überwachung stoppen" : "Live-Überwachung starten";

    [RelayCommand]
    private async Task ToggleMonitoringAsync()
    {
        if (IsMonitoring) { StopMonitoring(); return; }

        SensorStatus = "Initialisiere Sensorik …";
        SensorAvailable = await Task.Run(() => _sensors.IsAvailable).ConfigureAwait(true);
        if (!SensorAvailable)
        {
            SensorStatus = "Sensorik nicht verfügbar – Adminrechte nötig, oder die Hardware liefert keine Sensoren.";
            return;
        }

        IsMonitoring = true;
        SensorStatus = "Live-Überwachung aktiv.";
        _timer.Start();
    }

    private void StopMonitoring()
    {
        _timer.Stop();
        IsMonitoring = false;
        SensorStatus = "Überwachung gestoppt.";
    }

    [RelayCommand(CanExecute = nameof(CanStartStress))]
    private async Task StartStressAsync()
    {
        var minutes = Math.Clamp(StressDurationMinutes, 0.5, 60);
        var confirmed = _dialogs.Confirm("Stresstest starten",
            $"Der Stresstest belastet CPU und Arbeitsspeicher für {minutes:0.#} Minuten voll aus – " +
            "der PC wird dabei deutlich wärmer und lauter.\n\n" +
            "Sicherheit: Der Test stoppt automatisch, sobald CPU/GPU eine kritische Temperatur " +
            "erreichen, und kann jederzeit über „Stoppen“ abgebrochen werden.\n\n" +
            "Vor dem Test bitte sicherstellen, dass die Lüftung frei ist. Fortfahren?");
        if (!confirmed) return;

        var wasMonitoring = IsMonitoring;
        _timer.Stop();

        IsStressRunning = true;
        StressReport = null;
        OnPropertyChanged(nameof(HasReport));
        StressProgressPercent = 0;
        StressStatus = "Stresstest läuft …";

        var options = new StressTestOptions
        {
            Duration = TimeSpan.FromMinutes(Math.Clamp(StressDurationMinutes, 0.5, 60))
        };
        _stressCts = new CancellationTokenSource();
        var progress = new Progress<StressProgress>(OnStressProgress);

        try
        {
            // Sicherstellen, dass die Sensorik offen ist (für die Live-Werte im Test).
            SensorAvailable = await Task.Run(() => _sensors.IsAvailable).ConfigureAwait(true);
            var report = await _stress.RunAsync(options, progress, _stressCts.Token).ConfigureAwait(true);
            StressReport = report;
            OnPropertyChanged(nameof(HasReport));
            StressStatus = report.Verdict;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Stresstest fehlgeschlagen");
            StressStatus = $"Stresstest fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            IsStressRunning = false;
            _stressCts?.Dispose();
            _stressCts = null;
            if (wasMonitoring) _timer.Start();
        }
    }

    private bool CanStartStress() => !IsStressRunning;

    [RelayCommand]
    private void CancelStress() => _stressCts?.Cancel();

    private void OnStressProgress(StressProgress p)
    {
        StressProgressPercent = p.Total > TimeSpan.Zero
            ? Math.Clamp(p.Elapsed / p.Total * 100, 0, 100) : 0;
        StressStatus = $"Stresstest läuft … {p.Elapsed:mm\\:ss} / {p.Total:mm\\:ss}";
        ApplyReadings(p.Current);
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        try
        {
            var readings = await Task.Run(() => _sensors.Read()).ConfigureAwait(true);
            ApplyReadings(readings);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Live-Sensorabfrage fehlgeschlagen");
        }
    }

    private void ApplyReadings(IReadOnlyList<SensorReading> readings)
    {
        var cpuTemp = Max(readings, SensorKind.Temperature, IsCpu);
        HasCpuTemp = cpuTemp is not null;
        if (cpuTemp is double ct) { CpuTempC = Math.Round(ct, 0); Push(CpuTempHistory, ct); }

        var gpuTemp = Max(readings, SensorKind.Temperature, IsGpu);
        HasGpuTemp = gpuTemp is not null;
        if (gpuTemp is double gt) { GpuTempC = Math.Round(gt, 0); Push(GpuTempHistory, gt); }

        var cpuLoad = Max(readings, SensorKind.Load, IsCpu);
        HasCpuLoad = cpuLoad is not null;
        if (cpuLoad is double cl) CpuLoadPct = Math.Round(cl, 0);

        var fan = Max(readings, SensorKind.FanRpm, _ => true);
        HasFan = fan is not null;
        if (fan is double f) MaxFanRpm = Math.Round(f, 0);
    }

    private static void Push(ObservableCollection<double> series, double value)
    {
        series.Add(value);
        while (series.Count > HistoryLength) series.RemoveAt(0);
    }

    private static double? Max(IReadOnlyList<SensorReading> readings, SensorKind kind, Func<SensorReading, bool> filter)
    {
        double? max = null;
        foreach (var r in readings)
        {
            if (r.Kind != kind || !filter(r)) continue;
            if (max is null || r.Value > max) max = r.Value;
        }
        return max;
    }

    private static bool IsCpu(SensorReading r)
        => string.Equals(r.HardwareType, "Cpu", StringComparison.OrdinalIgnoreCase);

    private static bool IsGpu(SensorReading r)
        => r.HardwareType.StartsWith("Gpu", StringComparison.OrdinalIgnoreCase);

    partial void OnIsMonitoringChanged(bool value) => OnPropertyChanged(nameof(MonitoringButtonText));
    partial void OnIsStressRunningChanged(bool value) => StartStressCommand.NotifyCanExecuteChanged();
}
