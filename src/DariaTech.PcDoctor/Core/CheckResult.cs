namespace DariaTech.PcDoctor.Core;

/// <summary>
/// Ergebnis einer einzelnen Diagnose-Prüfung. UI-frei.
/// </summary>
/// <param name="Area">Bereich/Kachel, z. B. "Datenträger – Speicherplatz".</param>
/// <param name="Label">Kurzbezeichnung der Zeile, z. B. "Laufwerk C:".</param>
/// <param name="Value">Anzuzeigender Wert, z. B. "42,1 GB frei (12 %)".</param>
/// <param name="Severity">Ampel-Status.</param>
/// <param name="Detail">Optionaler Erklärtext für die Detailansicht.</param>
/// <param name="Fixes">Optional zugeordnete Reparaturen für dieses Ergebnis.</param>
/// <param name="Tip">
/// Optionaler Praxis-Tipp: wie man das Problem behebt und wo man die Stelle in
/// Windows findet (wird im Detail-Popup angezeigt).
/// </param>
/// <param name="OpenTarget">
/// Optionales Sprungziel in Windows (z. B. "ms-settings:windowsupdate",
/// "devmgmt.msc"). Ist es gesetzt, erscheint im Detail ein Button
/// „Hier öffnen", der den Nutzer direkt dorthin führt. Siehe
/// <see cref="SystemLauncher"/>.
/// </param>
public sealed record CheckResult(
    string Area,
    string Label,
    string Value,
    Severity Severity,
    string? Detail = null,
    IReadOnlyList<IFixAction>? Fixes = null,
    string? Tip = null,
    string? OpenTarget = null)
{
    /// <summary>True, wenn ein Praxis-Tipp hinterlegt ist.</summary>
    public bool HasTip => !string.IsNullOrWhiteSpace(Tip);

    /// <summary>True, wenn ein Windows-Sprungziel hinterlegt ist.</summary>
    public bool HasOpenTarget => !string.IsNullOrWhiteSpace(OpenTarget);
}
