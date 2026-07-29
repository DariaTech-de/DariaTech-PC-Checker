namespace DariaTech.PcDoctor.Core.Security;

/// <summary>
/// Regeln für zulässige PINs. Wird beim Bauen geprüft (damit kein zu kurzer PIN
/// in eine Auslieferung gelangt) und bei der Eingabe (Schaltfläche bleibt sonst
/// gesperrt). Rein funktional und damit vollständig testbar.
/// </summary>
public static class PinPolicy
{
    /// <summary>Mindestlänge – vorgegeben: 8 Zeichen.</summary>
    public const int MinimumLength = 8;

    /// <summary>Obergrenze, damit die Eingabemaske nicht missbraucht wird.</summary>
    public const int MaximumLength = 128;

    /// <summary>
    /// Prüft eine Eingabe. Liefert <c>null</c>, wenn sie zulässig ist – sonst den
    /// Grund im Klartext (wird in der Eingabemaske angezeigt).
    /// </summary>
    public static string? Validate(string? pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
            return "Bitte den PIN eingeben.";

        if (pin.Length < MinimumLength)
            return $"Der PIN muss mindestens {MinimumLength} Zeichen lang sein.";

        if (pin.Length > MaximumLength)
            return $"Der PIN darf höchstens {MaximumLength} Zeichen lang sein.";

        return null;
    }

    /// <summary>True, wenn die Eingabe der Richtlinie entspricht.</summary>
    public static bool IsAcceptable(string? pin) => Validate(pin) is null;

    /// <summary>
    /// Warnung für den Build-Vorgang: sehr einfache PINs (nur eine Ziffer
    /// wiederholt, fortlaufende Folgen) sind zwar lang genug, aber leicht zu
    /// erraten. Liefert <c>null</c>, wenn nichts zu beanstanden ist.
    /// </summary>
    public static string? WeaknessWarning(string? pin)
    {
        if (string.IsNullOrEmpty(pin)) return null;

        if (pin.Distinct().Count() == 1)
            return "Der PIN besteht aus nur einem sich wiederholenden Zeichen.";

        if (IsSequential(pin))
            return "Der PIN ist eine fortlaufende Folge (z. B. 12345678).";

        return null;
    }

    private static bool IsSequential(string pin)
    {
        if (pin.Length < 3) return false;

        var ascending = true;
        var descending = true;
        for (var i = 1; i < pin.Length; i++)
        {
            if (pin[i] != pin[i - 1] + 1) ascending = false;
            if (pin[i] != pin[i - 1] - 1) descending = false;
        }
        return ascending || descending;
    }
}
