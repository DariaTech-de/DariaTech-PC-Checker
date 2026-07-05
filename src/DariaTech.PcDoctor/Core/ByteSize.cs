namespace DariaTech.PcDoctor.Core;

/// <summary>Formatiert Byte-Größen menschenlesbar (B/KB/MB/GB/TB). Rein funktional.</summary>
public static class ByteSize
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public static string Human(long bytes)
    {
        if (bytes < 0) bytes = 0;
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} {Units[unit]}" : $"{value:N1} {Units[unit]}";
    }
}
