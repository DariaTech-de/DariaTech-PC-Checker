namespace DariaTech.PcDoctor.Core.Security;

/// <summary>Gespeicherter Zustand der Fehlversuchssperre (übersteht einen Neustart der App).</summary>
/// <param name="FailedAttempts">Anzahl aufeinanderfolgender Fehlversuche.</param>
/// <param name="LockedUntilUtc">Sperre gilt bis zu diesem Zeitpunkt (UTC), sonst null.</param>
public sealed record PinLockoutState(int FailedAttempts, DateTime? LockedUntilUtc)
{
    public static readonly PinLockoutState Fresh = new(0, null);
}

/// <summary>
/// Fehlversuchssperre: Nach mehreren Fehleingaben wird die Eingabe zunehmend
/// länger gesperrt. Damit ist Durchprobieren in der Anwendung aussichtslos.
///
/// Die Zeit wird als Parameter übergeben (nicht intern gelesen), damit sich das
/// Verhalten vollständig und ohne Warten testen lässt.
/// </summary>
public static class PinLockout
{
    /// <summary>Ab diesem Fehlversuch beginnt die Sperre.</summary>
    public const int AttemptsBeforeLockout = 5;

    /// <summary>Wartezeiten je weiterem Fehlversuch (danach bleibt es beim letzten Wert).</summary>
    private static readonly TimeSpan[] Delays =
    {
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(60),
    };

    /// <summary>True, wenn die Eingabe derzeit gesperrt ist.</summary>
    public static bool IsLocked(PinLockoutState state, DateTime utcNow)
        => state.LockedUntilUtc is DateTime until && until > utcNow;

    /// <summary>Restliche Sperrzeit (Null, wenn nicht gesperrt).</summary>
    public static TimeSpan RemainingLock(PinLockoutState state, DateTime utcNow)
        => IsLocked(state, utcNow) ? state.LockedUntilUtc!.Value - utcNow : TimeSpan.Zero;

    /// <summary>Neuer Zustand nach einem Fehlversuch.</summary>
    public static PinLockoutState RegisterFailure(PinLockoutState state, DateTime utcNow)
    {
        var attempts = state.FailedAttempts + 1;

        if (attempts < AttemptsBeforeLockout)
            return new PinLockoutState(attempts, null);

        var index = Math.Min(attempts - AttemptsBeforeLockout, Delays.Length - 1);
        return new PinLockoutState(attempts, utcNow + Delays[index]);
    }

    /// <summary>Nach erfolgreicher Eingabe wird der Zähler zurückgesetzt.</summary>
    public static PinLockoutState RegisterSuccess() => PinLockoutState.Fresh;

    /// <summary>Verbleibende Versuche bis zur nächsten Sperre.</summary>
    public static int AttemptsLeft(PinLockoutState state)
        => Math.Max(0, AttemptsBeforeLockout - state.FailedAttempts);

    /// <summary>Hinweistext für die Eingabemaske.</summary>
    public static string Describe(PinLockoutState state, DateTime utcNow)
    {
        if (IsLocked(state, utcNow))
        {
            var remaining = RemainingLock(state, utcNow);
            return remaining.TotalMinutes >= 1
                ? $"Zu viele Fehlversuche – gesperrt für noch {Math.Ceiling(remaining.TotalMinutes):0} Minute(n)."
                : $"Zu viele Fehlversuche – gesperrt für noch {Math.Ceiling(remaining.TotalSeconds):0} Sekunde(n).";
        }

        if (state.FailedAttempts == 0) return string.Empty;

        var left = AttemptsLeft(state);
        return left > 0
            ? $"Falscher PIN. Noch {left} Versuch(e) bis zur Sperre."
            : "Falscher PIN.";
    }
}
