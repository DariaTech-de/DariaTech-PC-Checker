using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Core.Clone;
using DariaTech.PcDoctor.Models;
using DariaTech.PcDoctor.UI.Services;
using Microsoft.Extensions.Logging;

namespace DariaTech.PcDoctor.UI.ViewModels;

/// <summary>
/// Klon-Assistent: listet physische Datenträger, lässt Quelle/Ziel sicher wählen,
/// zeigt Sicherheitsprüfung/Warnungen und startet – nach getippter Bestätigung –
/// das 1:1-Klonen über ddrescue. Jederzeit abbrechbar.
/// </summary>
public sealed partial class CloneViewModel : ObservableObject
{
    private readonly IPhysicalDiskService _disks;
    private readonly DiskCloneService _clone;
    private readonly IDialogService _dialogs;
    private readonly ILogger<CloneViewModel> _log;
    private CancellationTokenSource? _cts;

    public CloneViewModel(IPhysicalDiskService disks, DiskCloneService clone,
        IDialogService dialogs, ILogger<CloneViewModel> log)
    {
        _disks = disks;
        _clone = clone;
        _dialogs = dialogs;
        _log = log;
        var engine = _clone.FindEngine();
        EngineFound = engine.Found;
        EngineStatus = engine.Found
            ? $"Klon-Engine gefunden: {engine.Path}"
            : $"ddrescue.exe fehlt – bitte nach {engine.Path} legen. Ohne Engine ist kein Klonen möglich.";
    }

    public ObservableCollection<PhysicalDisk> Disks { get; } = new();
    public ObservableCollection<string> CloneLog { get; } = new();

    [ObservableProperty] private PhysicalDisk? _selectedSource;
    [ObservableProperty] private PhysicalDisk? _selectedTarget;
    [ObservableProperty] private string _confirmationText = string.Empty;
    [ObservableProperty] private string _validationText = string.Empty;
    [ObservableProperty] private string _warningText = string.Empty;
    [ObservableProperty] private bool _isCloning;
    [ObservableProperty] private string _cloneStatus = string.Empty;
    [ObservableProperty] private bool _engineFound;
    [ObservableProperty] private string _engineStatus = string.Empty;

    public string ConfirmHint => $"Zum Starten exakt „{CloneValidation.ConfirmWord}“ eintippen.";

    [RelayCommand]
    private async Task LoadDisksAsync()
    {
        CloneStatus = "Lese Datenträger …";
        var list = await Task.Run(() => _disks.Enumerate()).ConfigureAwait(true);
        Disks.Clear();
        foreach (var d in list) Disks.Add(d);
        CloneStatus = $"{Disks.Count} Datenträger gefunden.";
        Revalidate();
    }

    [RelayCommand(CanExecute = nameof(CanStartClone))]
    private async Task StartCloneAsync()
    {
        if (SelectedSource is null || SelectedTarget is null) return;

        var ok = _dialogs.Confirm("Klonen wirklich starten?",
            $"QUELLE:\n  {SelectedSource.Display}\n\n" +
            $"ZIEL (wird VOLLSTÄNDIG gelöscht!):\n  {SelectedTarget.Display}\n\n" +
            "Dieser Vorgang überschreibt das Ziel unwiderruflich. Fortfahren?");
        if (!ok) return;

        IsCloning = true;
        StartCloneCommand.NotifyCanExecuteChanged();
        CloneLog.Clear();
        _cts = new CancellationTokenSource();
        var progress = new Progress<string>(s => CloneLog.Add(s));

        FixOutcome outcome;
        try
        {
            outcome = await _clone.CloneAsync(SelectedSource, SelectedTarget, progress, _cts.Token)
                .ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            outcome = new FixOutcome(false, "Klonen abgebrochen.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Klonen fehlgeschlagen");
            outcome = new FixOutcome(false, ex.Message);
        }
        finally
        {
            IsCloning = false;
            _cts?.Dispose();
            _cts = null;
            ConfirmationText = string.Empty;
            StartCloneCommand.NotifyCanExecuteChanged();
        }

        CloneStatus = outcome.Message;
        _dialogs.Inform(outcome.Success ? "Klonen abgeschlossen" : "Klonen beendet", outcome.Message);
    }

    private bool CanStartClone()
        => !IsCloning
           && EngineFound
           && DiskCloneValidator.Validate(SelectedSource, SelectedTarget).CanClone
           && string.Equals(ConfirmationText?.Trim(), CloneValidation.ConfirmWord, StringComparison.Ordinal);

    [RelayCommand]
    private void CancelClone() => _cts?.Cancel();

    partial void OnSelectedSourceChanged(PhysicalDisk? value) => Revalidate();
    partial void OnSelectedTargetChanged(PhysicalDisk? value) => Revalidate();
    partial void OnConfirmationTextChanged(string value) => StartCloneCommand.NotifyCanExecuteChanged();
    partial void OnIsCloningChanged(bool value) => StartCloneCommand.NotifyCanExecuteChanged();

    private void Revalidate()
    {
        var v = DiskCloneValidator.Validate(SelectedSource, SelectedTarget);
        ValidationText = string.Join("\n", v.Errors);
        WarningText = string.Join("\n", v.Warnings);
        StartCloneCommand.NotifyCanExecuteChanged();
    }
}
