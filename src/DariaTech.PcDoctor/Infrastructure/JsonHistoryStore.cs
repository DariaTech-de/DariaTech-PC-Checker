using System.IO;
using System.Text.Json;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Models;
using Microsoft.Extensions.Logging;

namespace DariaTech.PcDoctor.Infrastructure;

/// <summary>
/// Verlaufsspeicher auf JSON-Basis. Liegt neben der Anwendung (portabel auf dem
/// USB-Stick): Ordner <c>DariaTech-Verlauf</c> mit <c>history.json</c> als Index
/// und den HTML-Berichten je Befund.
/// </summary>
public sealed class JsonHistoryStore : IHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ReportExporter _exporter;
    private readonly ILogger<JsonHistoryStore> _log;
    private readonly string _folder;
    private readonly string _indexPath;
    private readonly object _gate = new();
    private readonly List<HistoryEntry> _entries;

    public JsonHistoryStore(ReportExporter exporter, ILogger<JsonHistoryStore> log)
    {
        _exporter = exporter;
        _log = log;
        _folder = Path.Combine(AppContext.BaseDirectory, "DariaTech-Verlauf");
        _indexPath = Path.Combine(_folder, "history.json");
        _entries = Load();
    }

    public IReadOnlyList<HistoryEntry> All()
    {
        lock (_gate) return _entries.ToList();
    }

    public HistoryEntry Save(IReadOnlyList<CheckResult> results, ReportContext? context)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(_folder);
            var now = DateTime.Now;
            var reportPath = _exporter.Export(results, context, _folder, now);

            var entry = new HistoryEntry
            {
                Timestamp = now,
                Customer = context?.CustomerName ?? string.Empty,
                Order = context?.OrderNumber ?? string.Empty,
                Technician = context?.Technician ?? string.Empty,
                Computer = Environment.MachineName,
                HealthScore = ReportExporter.HealthScore(results),
                CriticalCount = results.Count(r => r.Severity == Severity.Critical),
                WarningCount = results.Count(r => r.Severity == Severity.Warning),
                ReportPath = reportPath
            };

            _entries.Insert(0, entry);
            Persist();
            return entry;
        }
    }

    private List<HistoryEntry> Load()
    {
        try
        {
            if (!File.Exists(_indexPath)) return new List<HistoryEntry>();
            var json = File.ReadAllText(_indexPath);
            var list = JsonSerializer.Deserialize<List<HistoryEntry>>(json) ?? new List<HistoryEntry>();
            return list.OrderByDescending(e => e.Timestamp).ToList();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Verlauf konnte nicht geladen werden");
            return new List<HistoryEntry>();
        }
    }

    private void Persist()
    {
        try
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(_indexPath, JsonSerializer.Serialize(_entries, JsonOptions));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Verlauf konnte nicht gespeichert werden");
        }
    }
}
