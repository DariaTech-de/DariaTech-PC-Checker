using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Models;

namespace DariaTech.PcDoctor.UI.ViewModels;

/// <summary>
/// Kundenverlauf: zeigt gespeicherte Befunde (neueste zuerst), filterbar nach
/// Kunde, mit Score-Trend und „Bericht öffnen".
/// </summary>
public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly IHistoryStore _store;

    public HistoryViewModel(IHistoryStore store)
    {
        _store = store;
        Refresh();
    }

    public ObservableCollection<HistoryEntry> Entries { get; } = new();

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private string _trend = string.Empty;
    [ObservableProperty] private bool _isEmpty = true;

    public void Refresh()
    {
        var all = _store.All();
        var filtered = string.IsNullOrWhiteSpace(FilterText)
            ? all
            : all.Where(e => e.Customer.Contains(FilterText, StringComparison.OrdinalIgnoreCase)).ToList();

        Entries.Clear();
        foreach (var e in filtered) Entries.Add(e);
        IsEmpty = Entries.Count == 0;
        UpdateTrend(filtered);
    }

    /// <summary>Speichert den aktuellen Befund im Verlauf und aktualisiert die Liste.</summary>
    public HistoryEntry Save(IReadOnlyList<CheckResult> results, ReportContext? context)
    {
        var entry = _store.Save(results, context);
        Refresh();
        return entry;
    }

    [RelayCommand]
    private void OpenReport(HistoryEntry? entry)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.ReportPath)) return;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(entry.ReportPath) { UseShellExecute = true });
        }
        catch { /* Öffnen optional */ }
    }

    [RelayCommand]
    private void RefreshList() => Refresh();

    partial void OnFilterTextChanged(string value) => Refresh();

    private void UpdateTrend(IReadOnlyList<HistoryEntry> entries)
    {
        // Trend nur sinnvoll bei aktivem Kundenfilter und mehreren Einträgen.
        if (string.IsNullOrWhiteSpace(FilterText) || entries.Count < 2)
        {
            Trend = string.Empty;
            return;
        }
        var scores = entries.OrderBy(e => e.Timestamp).Select(e => e.HealthScore);
        Trend = "Score-Verlauf: " + string.Join(" → ", scores);
    }
}
