namespace DariaTech.PcDoctor.Core.Security;

/// <summary>
/// Sitzungsverwaltung für den PIN-Schutz: Nach 30 Minuten ohne Bedienung wird
/// die Anwendung wieder gesperrt, damit ein unbeaufsichtigtes Notebook beim
/// Kunden nicht offen zugänglich bleibt.
///
/// Die Zeit wird von außen übergeben, damit das Verhalten ohne Warten testbar ist.
/// </summary>
public sealed class PinSession
{
    /// <summary>Vorgegebene Untätigkeitsdauer bis zur erneuten Sperre.</summary>
    public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromMinutes(30);

    private readonly TimeSpan _idleTimeout;
    private DateTime? _lastActivityUtc;

    public PinSession(TimeSpan? idleTimeout = null)
        => _idleTimeout = idleTimeout ?? DefaultIdleTimeout;

    /// <summary>True, solange die Anwendung entsperrt ist.</summary>
    public bool IsUnlocked { get; private set; }

    /// <summary>Eingestellte Untätigkeitsdauer.</summary>
    public TimeSpan IdleTimeout => _idleTimeout;

    /// <summary>Nach erfolgreicher PIN-Eingabe aufrufen.</summary>
    public void Unlock(DateTime utcNow)
    {
        IsUnlocked = true;
        _lastActivityUtc = utcNow;
    }

    /// <summary>Sperrt sofort (z. B. beim Zeitablauf oder auf Wunsch).</summary>
    public void Lock()
    {
        IsUnlocked = false;
        _lastActivityUtc = null;
    }

    /// <summary>Bei jeder Bedienung aufrufen – setzt die Untätigkeitsuhr zurück.</summary>
    public void RegisterActivity(DateTime utcNow)
    {
        if (IsUnlocked) _lastActivityUtc = utcNow;
    }

    /// <summary>True, wenn die Untätigkeitsdauer überschritten ist.</summary>
    public bool IsIdleTimeoutReached(DateTime utcNow)
        => IsUnlocked && _lastActivityUtc is DateTime last && utcNow - last >= _idleTimeout;

    /// <summary>
    /// Prüft den Zeitablauf und sperrt bei Überschreitung. Liefert <c>true</c>,
    /// wenn dadurch gesperrt wurde (die UI zeigt dann die PIN-Abfrage erneut).
    /// </summary>
    public bool LockIfIdleTimeoutReached(DateTime utcNow)
    {
        if (!IsIdleTimeoutReached(utcNow)) return false;
        Lock();
        return true;
    }

    /// <summary>Verbleibende Zeit bis zur automatischen Sperre.</summary>
    public TimeSpan RemainingIdleTime(DateTime utcNow)
    {
        if (!IsUnlocked || _lastActivityUtc is not DateTime last) return TimeSpan.Zero;
        var remaining = _idleTimeout - (utcNow - last);
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}
