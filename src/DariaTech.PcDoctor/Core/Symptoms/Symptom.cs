namespace DariaTech.PcDoctor.Core.Symptoms;

/// <summary>
/// Ein Kundensymptom („Was geht nicht?") mit den dafür relevanten Prüfbereichen
/// und den empfohlenen Reparaturen in sinnvoller Reihenfolge. UI-frei.
/// </summary>
/// <param name="Id">Stabiler technischer Schlüssel.</param>
/// <param name="Title">Kurztitel für die Auswahlliste.</param>
/// <param name="Question">Formulierung, wie Kunden das Problem beschreiben.</param>
/// <param name="CheckAreas">
/// Bereiche (<see cref="ICheck.Area"/>), die für dieses Symptom geprüft werden –
/// exakt die Schreibweise der jeweiligen Prüfung.
/// </param>
/// <param name="FixTypes">
/// Empfohlene Reparaturen als Typ, in der Reihenfolge „zuerst das Harmloseste".
/// </param>
/// <param name="Advice">Vorgehenshinweis für den Techniker.</param>
public sealed record Symptom(
    string Id,
    string Title,
    string Question,
    IReadOnlyList<string> CheckAreas,
    IReadOnlyList<Type> FixTypes,
    string Advice);
