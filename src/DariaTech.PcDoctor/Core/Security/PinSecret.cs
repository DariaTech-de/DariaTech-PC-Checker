using System.Reflection;

namespace DariaTech.PcDoctor.Core.Security;

/// <summary>
/// Liefert den beim Bauen eingesetzten PIN-Hash. Die Werte stehen NICHT im
/// Quellcode, sondern werden beim Erstellen der <c>.exe</c> als Assembly-Metadaten
/// eingebettet (siehe <c>build/publish.ps1 -Pin</c>).
///
/// Warum eingebettet und nicht in einer Datei daneben? Eine Konfigurationsdatei
/// ließe sich einfach durch eine mit eigenem PIN ersetzen – das wäre ein offenes
/// Tor. In der Programmdatei ist der Hash Teil des signierten Programms.
///
/// Ist kein PIN eingebettet (Entwickler-Build), läuft die Anwendung ungeschützt.
/// Das ist beabsichtigt: Ein fehlender PIN darf niemals bedeuten, dass sich die
/// Anwendung gar nicht mehr starten lässt.
/// </summary>
public static class PinSecret
{
    private const string SaltKey = "DariaTech.PinSalt";
    private const string HashKey = "DariaTech.PinHash";
    private const string IterationsKey = "DariaTech.PinIterations";

    private static readonly Lazy<(string? Salt, string? Hash, int Iterations)> Values = new(Read);

    /// <summary>Zufallssalz (Base64) oder <c>null</c>.</summary>
    public static string? SaltBase64 => Values.Value.Salt;

    /// <summary>PIN-Hash (Base64) oder <c>null</c>.</summary>
    public static string? HashBase64 => Values.Value.Hash;

    /// <summary>Rundenzahl des Hash-Verfahrens.</summary>
    public static int Iterations => Values.Value.Iterations;

    /// <summary>True, wenn dieser Build mit einem PIN geschützt ist.</summary>
    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SaltBase64) && !string.IsNullOrWhiteSpace(HashBase64);

    /// <summary>Prüft eine Eingabe gegen den eingebetteten Hash.</summary>
    public static bool Verify(string? input)
        => IsConfigured && PinHasher.Verify(input, SaltBase64, HashBase64, Iterations);

    private static (string?, string?, int) Read()
    {
        string? salt = null, hash = null;
        var iterations = PinHasher.DefaultIterations;

        try
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            foreach (var attribute in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
            {
                switch (attribute.Key)
                {
                    case SaltKey: salt = attribute.Value; break;
                    case HashKey: hash = attribute.Value; break;
                    case IterationsKey:
                        if (int.TryParse(attribute.Value, out var parsed) && parsed > 0)
                            iterations = parsed;
                        break;
                }
            }
        }
        catch
        {
            // Ohne lesbare Metadaten gilt: kein PIN konfiguriert.
        }

        return (salt, hash, iterations);
    }
}
