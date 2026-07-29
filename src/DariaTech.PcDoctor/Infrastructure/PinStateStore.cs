using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DariaTech.PcDoctor.Core.Security;
using Microsoft.Extensions.Logging;

namespace DariaTech.PcDoctor.Infrastructure;

/// <summary>
/// Speichert den Zustand der Fehlversuchssperre, damit ein Neustart der
/// Anwendung den Zähler nicht zurücksetzt – sonst wäre die Sperre wirkungslos.
///
/// Die Datei wird mit einem HMAC gesichert (Schlüssel ist das eingebettete Salz):
/// Verändert jemand den Inhalt von Hand, fällt das auf und der Eintrag wird
/// verworfen. Ehrliche Einschränkung: Wer die Datei löscht, setzt den Zähler
/// zurück – das lässt sich in einer portablen Anwendung ohne Server nicht
/// verhindern. Die Sperre bremst Rateversuche wirksam, sie ist kein Tresor.
/// </summary>
public sealed class PinStateStore
{
    private readonly ILogger<PinStateStore>? _log;
    private readonly string _path;

    public PinStateStore(ILogger<PinStateStore>? log = null)
    {
        _log = log;
        _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DariaTech", "PC-Doktor", "zugang.dat");
    }

    /// <summary>Gespeicherten Zustand lesen. Bei Fehlern/Manipulation: unbelasteter Zustand.</summary>
    public PinLockoutState Load()
    {
        try
        {
            if (!File.Exists(_path)) return PinLockoutState.Fresh;

            var content = File.ReadAllText(_path);
            var separator = content.LastIndexOf('|');
            if (separator <= 0) return PinLockoutState.Fresh;

            var payload = content[..separator];
            var mac = content[(separator + 1)..];

            if (!FixedTimeEquals(mac, ComputeMac(payload)))
            {
                _log?.LogWarning("Zugangsdatei wurde verändert – Zustand wird verworfen.");
                return PinLockoutState.Fresh;
            }

            var stored = JsonSerializer.Deserialize<StoredState>(payload);
            if (stored is null) return PinLockoutState.Fresh;

            return new PinLockoutState(
                Math.Max(0, stored.FailedAttempts),
                stored.LockedUntilUtc);
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Zugangsdatei konnte nicht gelesen werden.");
            return PinLockoutState.Fresh;
        }
    }

    /// <summary>Zustand speichern (best effort – ein Schreibfehler darf die App nie blockieren).</summary>
    public void Save(PinLockoutState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var payload = JsonSerializer.Serialize(
                new StoredState(state.FailedAttempts, state.LockedUntilUtc));
            File.WriteAllText(_path, payload + "|" + ComputeMac(payload), new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Zugangsdatei konnte nicht geschrieben werden.");
        }
    }

    /// <summary>HMAC über den Inhalt – Schlüssel ist das eingebettete Salz des Builds.</summary>
    private static string ComputeMac(string payload)
    {
        var key = Encoding.UTF8.GetBytes(PinSecret.SaltBase64 ?? "DariaTech-PC-Doktor");
        using var hmac = new HMACSHA256(key);
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var left = Encoding.UTF8.GetBytes(a);
        var right = Encoding.UTF8.GetBytes(b);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    private sealed record StoredState(int FailedAttempts, DateTime? LockedUntilUtc);
}
