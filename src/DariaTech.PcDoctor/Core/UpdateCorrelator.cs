namespace DariaTech.PcDoctor.Core;

/// <summary>Ein installiertes Windows-Update.</summary>
public sealed record InstalledUpdate(string HotFixId, DateTime InstalledOn);

/// <summary>
/// Stellt den zeitlichen Zusammenhang zwischen einem installierten Update und dem
/// Beginn von Abstürzen her – beantwortet die Frage „Lag es am letzten Update?"
/// objektiv statt nach Gefühl. Rein funktional und damit gut testbar.
/// </summary>
public static class UpdateCorrelator
{
    /// <summary>Zeitfenster: Abstürze gelten als update-verdächtig, wenn sie so kurz danach beginnen.</summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(3);

    /// <summary>
    /// Liefert das Update, das kurz vor <paramref name="crashOnset"/> installiert
    /// wurde (das jüngste passende), oder <c>null</c>, wenn keines im Fenster liegt.
    /// </summary>
    public static InstalledUpdate? FindSuspect(
        IEnumerable<InstalledUpdate> updates,
        DateTime? crashOnset,
        TimeSpan? window = null)
    {
        if (crashOnset is not DateTime onset) return null;
        var w = window ?? DefaultWindow;

        return updates
            .Where(u => u.InstalledOn <= onset && onset - u.InstalledOn <= w)
            .OrderByDescending(u => u.InstalledOn)
            .FirstOrDefault();
    }

    /// <summary>
    /// Beginn des relevanten Absturzmusters: frühester Absturz jener Gruppen, die
    /// oft genug auftreten, um ein echtes Muster zu sein. <c>null</c>, wenn es
    /// kein solches Muster gibt.
    /// </summary>
    public static DateTime? CrashOnset(IEnumerable<CrashGroup> groups)
    {
        DateTime? onset = null;
        foreach (var g in groups)
        {
            if (g.Count < AppCrashAnalyzer.WarnCount) continue;
            if (onset is null || g.First < onset) onset = g.First;
        }
        return onset;
    }
}
