using System.Management;

namespace DariaTech.PcDoctor.Infrastructure;

/// <summary>
/// Legt vor systemverändernden Reparaturen einen Systemwiederherstellungspunkt
/// an (WMI-Klasse <c>SystemRestore</c> im Namespace <c>root\default</c>).
/// Schlägt das Anlegen fehl (z. B. Systemschutz deaktiviert), wird das gemeldet,
/// ohne zu werfen – die aufrufende Schicht entscheidet über das weitere Vorgehen.
/// </summary>
public sealed class RestorePointService
{
    // RestorePointType: APPLICATION_INSTALL = 0, MODIFY_SETTINGS = 12
    private const int ModifySettings = 12;
    private const int BeginSystemChange = 100;

    public Task<FixOutcomeLite> CreateAsync(string description, CancellationToken ct = default)
        => Task.Run(() => Create(description), ct);

    private static FixOutcomeLite Create(string description)
    {
        try
        {
            using var management = new ManagementClass(
                @"\\.\root\default", "SystemRestore", null);

            var inParams = management.GetMethodParameters("CreateRestorePoint");
            inParams["Description"] = description;
            inParams["RestorePointType"] = ModifySettings;
            inParams["EventType"] = BeginSystemChange;

            var outParams = management.InvokeMethod("CreateRestorePoint", inParams, null);
            var returnValue = Convert.ToInt32(outParams["ReturnValue"] ?? -1);

            return returnValue == 0
                ? new FixOutcomeLite(true, "Wiederherstellungspunkt angelegt.")
                : new FixOutcomeLite(false,
                    $"Wiederherstellungspunkt nicht angelegt (Code {returnValue}). " +
                    "Möglicherweise ist der Systemschutz deaktiviert.");
        }
        catch (Exception ex)
        {
            return new FixOutcomeLite(false,
                $"Wiederherstellungspunkt konnte nicht angelegt werden: {ex.Message}");
        }
    }
}

/// <summary>Schlankes Ergebnis ohne Abhängigkeit zum Core-Namespace.</summary>
public sealed record FixOutcomeLite(bool Success, string Message);
