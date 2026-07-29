using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DariaTech.PcDoctor.Core.Security;
using DariaTech.PcDoctor.Infrastructure;
using DariaTech.PcDoctor.Models;
using Microsoft.Extensions.Logging;

namespace DariaTech.PcDoctor.UI.ViewModels;

/// <summary>
/// Zugangsschutz: prüft den eingegebenen PIN gegen den im Build eingebetteten
/// Hash und verwaltet die Fehlversuchssperre. Der PIN wird nur zum Prüfen
/// verwendet und danach verworfen – er wird nirgends gespeichert oder
/// protokolliert.
/// </summary>
public sealed partial class PinViewModel : ObservableObject
{
    private readonly PinStateStore _store;
    private readonly ILogger<PinViewModel>? _log;

    public PinViewModel(PinStateStore store, ILogger<PinViewModel>? log = null)
    {
        _store = store;
        _log = log;
        _lockoutState = store.Load();
        UpdateStatus();
    }

    private PinLockoutState _lockoutState;

    /// <summary>Wird gesetzt, sobald der PIN korrekt eingegeben wurde.</summary>
    [ObservableProperty] private bool _isAuthenticated;

    /// <summary>Eingegebener PIN (wird von der Ansicht bei jeder Eingabe aktualisiert).</summary>
    [ObservableProperty] private string _pin = string.Empty;

    /// <summary>Hinweistext (Fehlversuche, Sperre, Eingabefehler).</summary>
    [ObservableProperty] private string _status = string.Empty;

    /// <summary>True, wenn die Eingabe gerade gesperrt ist.</summary>
    [ObservableProperty] private bool _isLocked;

    /// <summary>Eingabefeld und Schaltfläche sind nur nutzbar, wenn nicht gesperrt.</summary>
    public bool IsInputEnabled => !IsLocked;

    /// <summary>Überschrift – unterscheidet Erststart und erneute Sperre nach Zeitablauf.</summary>
    [ObservableProperty] private string _headline = "Zugang zum DariaTech PC-Doktor";

    /// <summary>Wird für die erneute Abfrage nach Zeitablauf verwendet.</summary>
    public void SwitchToRelockMode()
    {
        Headline = "Sitzung gesperrt – bitte PIN erneut eingeben";
        Status = $"Die Anwendung wurde nach {PinSession.DefaultIdleTimeout.TotalMinutes:0} Minuten " +
                 "ohne Bedienung automatisch gesperrt.";
        Pin = string.Empty;
        IsAuthenticated = false;
    }

    [RelayCommand]
    private void Submit()
    {
        var now = DateTime.UtcNow;

        // 1. Sperre beachten
        if (PinLockout.IsLocked(_lockoutState, now))
        {
            IsLocked = true;
            Status = PinLockout.Describe(_lockoutState, now);
            return;
        }
        IsLocked = false;

        // 2. Eingabe formal prüfen (verhindert sinnlose, teure Hash-Berechnungen)
        if (PinPolicy.Validate(Pin) is string problem)
        {
            Status = problem;
            return;
        }

        // 3. Gegen den eingebetteten Hash prüfen
        if (PinSecret.Verify(Pin))
        {
            _lockoutState = PinLockout.RegisterSuccess();
            _store.Save(_lockoutState);
            Pin = string.Empty;
            Status = string.Empty;
            IsAuthenticated = true;
            _log?.LogInformation("Zugang gewährt.");
            return;
        }

        // 4. Fehlversuch verbuchen und dauerhaft speichern
        _lockoutState = PinLockout.RegisterFailure(_lockoutState, now);
        _store.Save(_lockoutState);
        Pin = string.Empty;
        IsLocked = PinLockout.IsLocked(_lockoutState, now);
        Status = PinLockout.Describe(_lockoutState, now);
        _log?.LogWarning("Fehlgeschlagene PIN-Eingabe ({Attempts}. Versuch).", _lockoutState.FailedAttempts);
    }

    /// <summary>Aktualisiert Sperrhinweis (z. B. durch einen Sekundentakt der Ansicht).</summary>
    public void Refresh()
    {
        var now = DateTime.UtcNow;
        var locked = PinLockout.IsLocked(_lockoutState, now);
        IsLocked = locked;
        if (locked) Status = PinLockout.Describe(_lockoutState, now);
        else if (_lockoutState.FailedAttempts >= PinLockout.AttemptsBeforeLockout)
            Status = "Sperre abgelaufen – erneute Eingabe möglich.";
    }

    private void UpdateStatus()
    {
        var now = DateTime.UtcNow;
        IsLocked = PinLockout.IsLocked(_lockoutState, now);
        Status = PinLockout.Describe(_lockoutState, now);
    }

    /// <summary>Fußzeile der Eingabemaske.</summary>
    public string Footer => $"{CompanyInfo.ProductFull} {AppInfo.Version} · nur für autorisierte Techniker";

    partial void OnIsLockedChanged(bool value) => OnPropertyChanged(nameof(IsInputEnabled));
}
