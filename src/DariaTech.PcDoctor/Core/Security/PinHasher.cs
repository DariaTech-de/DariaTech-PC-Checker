using System.Security.Cryptography;
using System.Text;

namespace DariaTech.PcDoctor.Core.Security;

/// <summary>
/// Prüft den Zugangs-PIN. Der PIN selbst wird NIE gespeichert – weder im
/// Quellcode noch in der <c>.exe</c>. Abgelegt ist nur ein PBKDF2-Hash mit
/// Zufallssalz; daraus lässt sich der PIN nicht zurückrechnen.
///
/// Bewusste Entscheidungen:
/// - <b>PBKDF2 mit SHA-256 und hoher Rundenzahl</b>: macht das Durchprobieren
///   teuer. Jede Prüfung kostet spürbar Rechenzeit, was bei einem Angriff mit
///   Millionen Versuchen entscheidend ist.
/// - <b>Zeitkonstanter Vergleich</b> (<see cref="CryptographicOperations.FixedTimeEquals"/>):
///   verhindert, dass sich der PIN über Laufzeitunterschiede erraten lässt.
/// - <b>Zufallssalz</b>: zwei gleiche PINs ergeben verschiedene Hashes, und
///   vorberechnete Tabellen sind wertlos.
///
/// Ehrliche Einordnung: Eine Anwendung, die auf fremder Hardware läuft, lässt
/// sich mit Reverse Engineering grundsätzlich umgehen. Dieser Schutz verhindert
/// zuverlässig die Bedienung durch Unbefugte – er ist kein Kopierschutz.
/// </summary>
public static class PinHasher
{
    /// <summary>Rundenzahl für PBKDF2. Bewusst hoch, um Durchprobieren zu verlangsamen.</summary>
    public const int DefaultIterations = 600_000;

    /// <summary>Länge des Zufallssalzes in Bytes.</summary>
    public const int SaltBytes = 16;

    /// <summary>Länge des abgeleiteten Schlüssels in Bytes.</summary>
    public const int HashBytes = 32;

    /// <summary>Erzeugt ein neues Zufallssalz.</summary>
    public static byte[] CreateSalt() => RandomNumberGenerator.GetBytes(SaltBytes);

    /// <summary>Leitet den Hash aus PIN und Salz ab.</summary>
    public static byte[] Derive(string pin, byte[] salt, int iterations = DefaultIterations)
    {
        if (pin is null) throw new ArgumentNullException(nameof(pin));
        if (salt is null || salt.Length == 0) throw new ArgumentException("Salz fehlt.", nameof(salt));
        if (iterations < 1) throw new ArgumentOutOfRangeException(nameof(iterations));

        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(pin),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashBytes);
    }

    /// <summary>
    /// Prüft eine Eingabe gegen den gespeicherten Hash. Liefert bei fehlender
    /// oder fehlerhafter Konfiguration <c>false</c> – niemals versehentlich <c>true</c>.
    /// </summary>
    public static bool Verify(string? input, string? saltBase64, string? hashBase64,
        int iterations = DefaultIterations)
    {
        if (string.IsNullOrEmpty(input) ||
            string.IsNullOrWhiteSpace(saltBase64) ||
            string.IsNullOrWhiteSpace(hashBase64)) return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(saltBase64);
            expected = Convert.FromBase64String(hashBase64);
        }
        catch (FormatException)
        {
            return false;   // beschädigte Konfiguration -> kein Zugang
        }

        if (salt.Length == 0 || expected.Length == 0) return false;

        var actual = Derive(input, salt, iterations);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>
    /// Erzeugt Salz und Hash für einen neuen PIN – wird beim Bauen verwendet,
    /// um die Werte in die Anwendung einzusetzen.
    /// </summary>
    public static (string SaltBase64, string HashBase64, int Iterations) CreateSecret(
        string pin, int iterations = DefaultIterations)
    {
        var salt = CreateSalt();
        var hash = Derive(pin, salt, iterations);
        return (Convert.ToBase64String(salt), Convert.ToBase64String(hash), iterations);
    }
}
