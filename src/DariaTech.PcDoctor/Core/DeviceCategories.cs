namespace DariaTech.PcDoctor.Core;

/// <summary>
/// Ordnet Windows-Geräteklassen (<c>Win32_PnPEntity.PNPClass</c>) den Kategorien
/// zu, nach denen Kunden ihre Probleme beschreiben („kein Ton“, „Bluetooth geht
/// nicht“, „Webcam wird nicht erkannt“). Rein funktional und damit testbar.
/// </summary>
public static class DeviceCategories
{
    public const string Audio = "Audio";
    public const string Bluetooth = "Bluetooth";
    public const string Camera = "Kamera";
    public const string Usb = "USB";
    public const string Display = "Grafik & Bildschirm";
    public const string Network = "Netzwerkadapter";
    public const string Printer = "Drucker";
    public const string Input = "Tastatur & Maus";
    public const string Power = "Akku & Energie";

    /// <summary>Kategorien, die der Assistent gezielt auswerten kann.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Audio, Bluetooth, Camera, Usb, Display, Network, Printer, Input, Power
    };

    /// <summary>
    /// Bestimmt die Kategorie eines Geräts. Liefert <c>null</c> für Geräte, die
    /// für die Fehlersuche uninteressant sind (System-, Software-Komponenten).
    /// </summary>
    public static string? Classify(string? pnpClass, string? name = null)
    {
        var cls = (pnpClass ?? string.Empty).Trim().ToLowerInvariant();

        var byClass = cls switch
        {
            "media" or "audioendpoint" or "sound" or "audioprocessingobject" => Audio,
            "bluetooth" => Bluetooth,
            "camera" or "image" => Camera,
            "usb" or "usbdevice" => Usb,
            "display" or "monitor" => Display,
            "net" => Network,
            "printer" or "printqueue" => Printer,
            "hidclass" or "keyboard" or "mouse" => Input,
            "battery" => Power,
            _ => null
        };
        if (byClass is not null) return byClass;

        // Ohne (oder mit unbekannter) Geräteklasse hilft der Name weiter.
        var text = (name ?? string.Empty).ToLowerInvariant();
        if (text.Length == 0) return null;

        if (Contains(text, "bluetooth")) return Bluetooth;
        if (Contains(text, "webcam", "kamera", "camera")) return Camera;
        if (Contains(text, "audio", "lautsprecher", "kopfhörer", "mikrofon", "microphone", "realtek high definition"))
            return Audio;
        if (Contains(text, "usb")) return Usb;
        if (Contains(text, "grafik", "graphics", "geforce", "radeon", "monitor", "bildschirm")) return Display;
        if (Contains(text, "drucker", "printer")) return Printer;
        if (Contains(text, "tastatur", "keyboard", "maus", "mouse")) return Input;
        if (Contains(text, "akku", "battery")) return Power;

        return null;
    }

    private static bool Contains(string text, params string[] needles)
        => needles.Any(n => text.Contains(n, StringComparison.Ordinal));

    /// <summary>Klartext zu den gängigsten Geräte-Manager-Fehlercodes.</summary>
    public static string CodeMeaning(int code) => code switch
    {
        1 => "Gerät nicht korrekt konfiguriert.",
        3 => "Treiber beschädigt oder zu wenig Arbeitsspeicher.",
        10 => "Gerät kann nicht gestartet werden – Treiber prüfen.",
        12 => "Nicht genügend freie Ressourcen.",
        18 => "Treiber muss neu installiert werden.",
        19 => "Registry-Konfiguration beschädigt.",
        22 => "Gerät ist DEAKTIVIERT – im Geräte-Manager aktivieren.",
        24 => "Gerät nicht vorhanden oder fehlerhaft.",
        28 => "Kein Treiber installiert.",
        31 => "Windows kann keinen passenden Treiber laden.",
        37 => "Treiber meldet einen Fehler.",
        39 => "Treiber beschädigt oder fehlt.",
        43 => "Hardware meldet ein Problem – möglicher Defekt.",
        45 => "Gerät derzeit nicht angeschlossen.",
        52 => "Treibersignatur nicht überprüfbar.",
        _ => "Treiber prüfen oder neu installieren."
    };

    /// <summary>
    /// Ein deaktiviertes Gerät (Code 22) ist die häufigste, am schnellsten
    /// behebbare Ursache – das soll dem Techniker sofort auffallen.
    /// </summary>
    public static bool IsDisabled(int code) => code == 22;
}
